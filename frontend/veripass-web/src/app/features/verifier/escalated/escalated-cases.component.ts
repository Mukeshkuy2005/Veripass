import { Component, inject, signal, OnInit, OnDestroy, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  VerifierEscalatedService,
  EscalatedCaseItem,
  EscalatedCasesSummary,
  EscalatedCaseDetail
} from '../../../core/services/verifier-escalated.service';
import { formatIstDateTime, formatRelativeTime } from '../../../core/utils/ist-date-formatter';

type TabView = 'all' | 'mine' | 'unassigned';

@Component({
  selector: 'vp-escalated-cases',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './escalated-cases.component.html',
  styleUrl: './escalated-cases.component.css'
})
export class EscalatedCasesComponent implements OnInit, OnDestroy {
  private escalatedService = inject(VerifierEscalatedService);

  // Queue state signals
  loading = signal<boolean>(false);
  error = signal<string | null>(null);
  items = signal<EscalatedCaseItem[]>([]);
  summary = signal<EscalatedCasesSummary>({ totalCount: 0, assignedCount: 0, unassignedCount: 0 });

  // View & Filter signals
  selectedTab = signal<TabView>('all');
  searchQuery = signal<string>('');
  priorityFilter = signal<string>('ALL');
  reasonFilter = signal<string>('ALL');
  dateRangeFilter = signal<string>('ALL');

  // Server-side Pagination
  page = signal<number>(1);
  pageSize = signal<number>(25);
  totalCount = signal<number>(0);
  totalPages = signal<number>(1);

  // Time & Live Ticker
  lastUpdatedIst = signal<string>('—');
  currentTime = signal<Date>(new Date());
  private timerId: any = null;

  // Slide-out Detail Drawer signals
  drawerOpen = signal<boolean>(false);
  selectedCaseId = signal<string | null>(null);
  detailLoading = signal<boolean>(false);
  detailError = signal<string | null>(null);
  caseDetail = signal<EscalatedCaseDetail | null>(null);

  // Senior Decision Action states
  actionSubmitting = signal<boolean>(false);
  actionSuccessMessage = signal<string | null>(null);
  actionErrorMessage = signal<string | null>(null);

  // Confirmation Modal states (for Reject and Return)
  confirmModalType = signal<'REJECT' | 'RETURN' | null>(null);
  modalReason = signal<string>('');
  modalReasonError = signal<string | null>(null);

  ngOnInit(): void {
    this.loadEscalatedCases();

    // Live relative time recalculation ticker every 30 seconds
    this.timerId = setInterval(() => {
      this.currentTime.set(new Date());
    }, 30000);
  }

  ngOnDestroy(): void {
    if (this.timerId) {
      clearInterval(this.timerId);
      this.timerId = null;
    }
  }

  // Keyboard accessibility: Escape closes drawer and modals
  @HostListener('document:keydown.escape')
  onEscapeKey(): void {
    if (this.confirmModalType()) {
      this.closeConfirmModal();
    } else if (this.drawerOpen()) {
      this.closeDrawer();
    }
  }

  loadEscalatedCases(): void {
    this.loading.set(true);
    this.error.set(null);

    // Calculate dates from dateRangeFilter
    let dateFrom: string | undefined;
    const now = new Date();
    if (this.dateRangeFilter() === 'TODAY') {
      const todayStart = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate(), 0, 0, 0));
      dateFrom = todayStart.toISOString();
    } else if (this.dateRangeFilter() === '7DAYS') {
      const d7 = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
      dateFrom = d7.toISOString();
    } else if (this.dateRangeFilter() === '30DAYS') {
      const d30 = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);
      dateFrom = d30.toISOString();
    }

    this.escalatedService.getEscalatedCases({
      view: this.selectedTab(),
      search: this.searchQuery(),
      priority: this.priorityFilter(),
      reason: this.reasonFilter(),
      dateFrom,
      page: this.page(),
      pageSize: this.pageSize(),
      sortBy: 'escalatedAt',
      sortDirection: 'desc'
    }).subscribe({
      next: (res) => {
        this.loading.set(false);
        this.items.set(res.items || []);
        this.summary.set(res.summary || { totalCount: 0, assignedCount: 0, unassignedCount: 0 });
        this.totalCount.set(res.totalCount || 0);
        this.totalPages.set(res.totalPages || 1);

        // Update real timestamp in IST
        const formatted = formatIstDateTime(res.serverTimeUtc || new Date().toISOString());
        this.lastUpdatedIst.set(formatted.timePart);
      },
      error: (err) => {
        this.loading.set(false);
        const msg = err?.error?.message || err?.message || 'Could not load escalated cases. Please check backend connection.';
        this.error.set(msg);
      }
    });
  }

  setTab(tab: TabView): void {
    if (this.selectedTab() === tab) return;
    this.selectedTab.set(tab);
    this.page.set(1);
    this.loadEscalatedCases();
  }

  applyFilters(): void {
    this.page.set(1);
    this.loadEscalatedCases();
  }

  resetFilters(): void {
    this.searchQuery.set('');
    this.priorityFilter.set('ALL');
    this.reasonFilter.set('ALL');
    this.dateRangeFilter.set('ALL');
    this.page.set(1);
    this.loadEscalatedCases();
  }

  onPageChange(newPage: number): void {
    if (newPage < 1 || newPage > this.totalPages() || newPage === this.page()) return;
    this.page.set(newPage);
    this.loadEscalatedCases();
  }

  // --- Detail Drawer Operations ---

  openDrawer(target: EscalatedCaseItem | string): void {
    const caseId = typeof target === 'string' ? target : target.caseId;
    this.selectedCaseId.set(caseId);
    this.drawerOpen.set(true);
    this.detailLoading.set(true);
    this.detailError.set(null);
    this.caseDetail.set(null);
    this.actionSuccessMessage.set(null);
    this.actionErrorMessage.set(null);

    this.escalatedService.getEscalationDetail(caseId).subscribe({
      next: (detail) => {
        this.detailLoading.set(false);
        this.caseDetail.set(detail);
      },
      error: (err) => {
        this.detailLoading.set(false);
        this.detailError.set(err?.error?.message || 'Could not load case details.');
      }
    });
  }

  retryDetail(): void {
    const id = this.selectedCaseId();
    if (id) {
      this.openDrawer(id);
    }
  }

  closeDrawer(): void {
    this.drawerOpen.set(false);
    this.selectedCaseId.set(null);
    this.caseDetail.set(null);
    this.actionSuccessMessage.set(null);
    this.actionErrorMessage.set(null);
  }

  // --- Senior Review Actions ---

  claimCase(item?: EscalatedCaseItem): void {
    const caseId = item ? item.caseId : this.caseDetail()?.caseId;
    const token = item ? item.concurrencyToken : this.caseDetail()?.concurrencyToken;
    if (!caseId) return;

    this.actionSubmitting.set(true);
    this.actionErrorMessage.set(null);

    this.escalatedService.claimCase(caseId, token).subscribe({
      next: () => {
        this.actionSubmitting.set(false);
        this.actionSuccessMessage.set('Case claimed successfully.');
        this.loadEscalatedCases();
        if (this.drawerOpen() && this.caseDetail()?.caseId === caseId) {
          // Re-fetch detail to refresh assignment status
          this.openDrawer({ caseId } as any);
        }
      },
      error: (err) => {
        this.actionSubmitting.set(false);
        const msg = err?.error?.message || 'Could not claim this case. It may have already been assigned or updated.';
        this.actionErrorMessage.set(msg);
        this.loadEscalatedCases();
      }
    });
  }

  approveCase(): void {
    const detail = this.caseDetail();
    if (!detail) return;

    this.actionSubmitting.set(true);
    this.actionErrorMessage.set(null);

    this.escalatedService.approveCase(detail.caseId, 'Senior review completed and approved.', detail.concurrencyToken).subscribe({
      next: () => {
        this.actionSubmitting.set(false);
        this.actionSuccessMessage.set('Case approved successfully.');
        setTimeout(() => {
          this.closeDrawer();
          this.loadEscalatedCases();
        }, 1200);
      },
      error: (err) => {
        this.actionSubmitting.set(false);
        const msg = err?.error?.message || 'Could not approve this case. The case may have changed or you may no longer have permission.';
        this.actionErrorMessage.set(msg);
        this.loadEscalatedCases();
      }
    });
  }

  openConfirmModal(type: 'REJECT' | 'RETURN'): void {
    this.confirmModalType.set(type);
    this.modalReason.set('');
    this.modalReasonError.set(null);
  }

  closeConfirmModal(): void {
    this.confirmModalType.set(null);
    this.modalReason.set('');
    this.modalReasonError.set(null);
  }

  submitModalDecision(): void {
    const detail = this.caseDetail();
    if (!detail) return;

    const reason = this.modalReason().trim();
    if (!reason) {
      this.modalReasonError.set('A valid reason is required.');
      return;
    }

    const type = this.confirmModalType();
    this.actionSubmitting.set(true);
    this.modalReasonError.set(null);

    if (type === 'REJECT') {
      this.escalatedService.rejectCase(detail.caseId, reason, detail.concurrencyToken).subscribe({
        next: () => {
          this.actionSubmitting.set(false);
          this.closeConfirmModal();
          this.actionSuccessMessage.set('Case rejected successfully.');
          setTimeout(() => {
            this.closeDrawer();
            this.loadEscalatedCases();
          }, 1200);
        },
        error: (err) => {
          this.actionSubmitting.set(false);
          const msg = err?.error?.message || 'Could not reject this case. The case may have changed or already been resolved.';
          this.modalReasonError.set(msg);
          this.loadEscalatedCases();
        }
      });
    } else if (type === 'RETURN') {
      this.escalatedService.returnCase(detail.caseId, reason, detail.concurrencyToken).subscribe({
        next: () => {
          this.actionSubmitting.set(false);
          this.closeConfirmModal();
          this.actionSuccessMessage.set('Case returned to verifier queue successfully.');
          setTimeout(() => {
            this.closeDrawer();
            this.loadEscalatedCases();
          }, 1200);
        },
        error: (err) => {
          this.actionSubmitting.set(false);
          const msg = err?.error?.message || 'Could not return this case. The case may have changed or already been resolved.';
          this.modalReasonError.set(msg);
          this.loadEscalatedCases();
        }
      });
    }
  }

  // --- Formatting Helpers ---

  formatIst(dateUtc: string | Date | undefined | null): string {
    return formatIstDateTime(dateUtc).fullFormatted;
  }

  formatRelative(dateUtc: string | Date | undefined | null): string {
    return formatRelativeTime(dateUtc, this.currentTime());
  }
}
