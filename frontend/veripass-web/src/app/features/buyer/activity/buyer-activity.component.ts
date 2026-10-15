import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { Subject, Subscription, debounceTime, distinctUntilChanged } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { BuyerActivityService } from './services/buyer-activity.service';
import {
  BuyerActivityDetail,
  BuyerActivityFilterState,
  BuyerActivityItem,
  BuyerActivitySummary,
  BuyerActivityUser
} from './models/buyer-activity.model';

@Component({
  selector: 'app-buyer-activity',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './buyer-activity.component.html',
  styleUrl: './buyer-activity.component.css'
})
export class BuyerActivityComponent implements OnInit, OnDestroy {
  private activityService = inject(BuyerActivityService);
  private auth = inject(AuthService);
  private searchSubject = new Subject<string>();
  private searchSubscription?: Subscription;

  // Reactive state
  isLoading = signal<boolean>(false);
  isError = signal<boolean>(false);
  errorMessage = signal<string>('');

  items = signal<BuyerActivityItem[]>([]);
  totalCount = signal<number>(0);
  totalPages = signal<number>(1);
  summary = signal<BuyerActivitySummary>({ today: 0, success: 0, failed: 0, denied: 0, total: 0 });
  users = signal<BuyerActivityUser[]>([]);

  selectedEvent = signal<BuyerActivityDetail | null>(null);
  isLoadingDetail = signal<boolean>(false);

  isExportDialogOpen = signal<boolean>(false);
  exportFormat = signal<'CSV' | 'JSON'>('CSV');
  isExporting = signal<boolean>(false);

  toastMessage = signal<string | null>(null);

  // Filter state
  filterState: BuyerActivityFilterState = {
    search: '',
    dateRange: 'last30',
    from: null,
    to: null,
    userId: 'all',
    category: 'all',
    action: 'all',
    target: '',
    result: 'all',
    sort: 'timestamp_desc',
    page: 1,
    pageSize: 25
  };

  // Controlled action options depending on Category
  readonly categoryOptions = [
    { value: 'all', label: 'All Categories' },
    { value: 'SUPPLIER', label: 'Supplier' },
    { value: 'PASSPORT', label: 'Passport' },
    { value: 'VERIFICATION', label: 'Verification' },
    { value: 'ONBOARDING', label: 'Onboarding' },
    { value: 'CREDENTIAL', label: 'Credential' },
    { value: 'USER', label: 'User' },
    { value: 'SECURITY', label: 'Security & Access' }
  ];

  readonly resultOptions = [
    { value: 'all', label: 'All Results' },
    { value: 'SUCCESS', label: 'Success' },
    { value: 'FAILED', label: 'Failed' },
    { value: 'DENIED', label: 'Denied' }
  ];

  readonly dateRangeOptions = [
    { value: 'today', label: 'Today' },
    { value: 'last7', label: 'Last 7 days' },
    { value: 'last30', label: 'Last 30 days' },
    { value: 'last90', label: 'Last 90 days' },
    { value: 'all', label: 'All Time' },
    { value: 'custom', label: 'Custom Range...' }
  ];

  ngOnInit(): void {
    this.searchSubscription = this.searchSubject
      .pipe(debounceTime(350), distinctUntilChanged())
      .subscribe(term => {
        this.filterState.search = term;
        this.filterState.page = 1;
        this.loadActivity();
      });

    this.loadSummary();
    this.loadUsers();
    this.loadActivity();
  }

  ngOnDestroy(): void {
    this.searchSubscription?.unsubscribe();
  }

  onSearchInput(value: string): void {
    this.searchSubject.next(value);
  }

  onFilterChange(): void {
    this.filterState.page = 1;
    this.loadActivity();
  }

  onCategoryChange(): void {
    this.filterState.action = 'all';
    this.filterState.page = 1;
    this.loadActivity();
  }

  clearFilters(): void {
    this.filterState = {
      search: '',
      dateRange: 'last30',
      from: null,
      to: null,
      userId: 'all',
      category: 'all',
      action: 'all',
      target: '',
      result: 'all',
      sort: 'timestamp_desc',
      page: 1,
      pageSize: 25
    };
    this.loadActivity();
  }

  loadActivity(): void {
    this.isLoading.set(true);
    this.isError.set(false);

    this.activityService.getActivity(this.filterState).subscribe({
      next: res => {
        this.items.set(res.items);
        this.totalCount.set(res.totalCount);
        this.totalPages.set(res.totalPages);
        this.isLoading.set(false);
      },
      error: err => {
        this.isLoading.set(false);
        this.isError.set(true);
        this.errorMessage.set(err?.error?.message || 'Unable to load audit activity. Please check network connection.');
      }
    });
  }

  loadSummary(): void {
    this.activityService.getSummary().subscribe({
      next: sum => this.summary.set(sum),
      error: () => {}
    });
  }

  loadUsers(): void {
    this.activityService.getUsers().subscribe({
      next: uList => this.users.set(uList),
      error: () => {}
    });
  }

  goToPage(p: number): void {
    if (p < 1 || p > this.totalPages()) return;
    this.filterState.page = p;
    this.loadActivity();
  }

  changePageSize(newSize: number): void {
    this.filterState.pageSize = newSize;
    this.filterState.page = 1;
    this.loadActivity();
  }

  selectEvent(item: BuyerActivityItem): void {
    this.isLoadingDetail.set(true);
    this.activityService.getDetail(item.id).subscribe({
      next: detail => {
        this.selectedEvent.set(detail);
        this.isLoadingDetail.set(false);
      },
      error: () => {
        this.isLoadingDetail.set(false);
        // Fallback to item projection
        this.selectedEvent.set({
          id: item.id,
          eventCode: item.eventCode,
          timestamp: item.timestamp,
          user: item.user,
          organization: {
            id: this.auth.currentUser()?.currentOrganizationId || '',
            name: this.auth.currentUser()?.companyName || this.auth.currentUser()?.organizationName || 'Buyer Organization'
          },
          action: item.action,
          actionLabel: item.actionLabel,
          category: item.category,
          target: item.target,
          result: item.result,
          resultLabel: item.resultLabel,
          securityContext: { isRestricted: true, requestId: 'REDACTED' }
        });
      }
    });
  }

  closeEventDrawer(): void {
    this.selectedEvent.set(null);
  }

  openExportDialog(): void {
    this.isExportDialogOpen.set(true);
  }

  closeExportDialog(): void {
    this.isExportDialogOpen.set(false);
  }

  executeExport(): void {
    this.isExporting.set(true);
    const { from, to } = this.resolveDateStrings();

    this.activityService.exportActivity({
      format: this.exportFormat(),
      search: this.filterState.search || undefined,
      from,
      to,
      userId: this.filterState.userId !== 'all' ? this.filterState.userId : undefined,
      category: this.filterState.category !== 'all' ? this.filterState.category : undefined,
      action: this.filterState.action !== 'all' ? this.filterState.action : undefined,
      target: this.filterState.target || undefined,
      result: this.filterState.result !== 'all' ? this.filterState.result : undefined
    }).subscribe({
      next: blob => {
        this.isExporting.set(false);
        this.closeExportDialog();

        const ext = this.exportFormat().toLowerCase();
        const timeStr = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19);
        const filename = `Buyer_Audit_Log_${timeStr}.${ext}`;

        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        a.click();
        window.URL.revokeObjectURL(url);

        this.showToast(`Audit log archive successfully exported as ${this.exportFormat()}. Operational event AUDIT_LOG_EXPORTED recorded.`);
        // Reload activity and summary to show new self-audit event
        this.loadSummary();
        this.loadActivity();
      },
      error: () => {
        this.isExporting.set(false);
        this.showToast('Export failed. Please check permissions and try again.');
      }
    });
  }

  showToast(msg: string): void {
    this.toastMessage.set(msg);
    setTimeout(() => this.toastMessage.set(null), 4000);
  }

  // Display formatting helpers
  formatTimeShort(ts: string): string {
    if (!ts) return '—';
    const date = new Date(ts);
    const now = new Date();
    const isToday = date.toDateString() === now.toDateString();
    const yesterday = new Date(now.setDate(now.getDate() - 1));
    const isYesterday = date.toDateString() === yesterday.toDateString();

    if (isToday) {
      return date.toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit', hour12: true });
    }
    if (isYesterday) {
      return 'Yesterday';
    }
    return date.toLocaleDateString('en-IN', { day: '2-digit', month: 'short' });
  }

  formatDateFull(ts: string): string {
    if (!ts) return '—';
    const d = new Date(ts);
    return d.toLocaleDateString('en-IN', {
      day: '2-digit',
      month: 'short',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      timeZoneName: 'short'
    });
  }

  getAvailableActions(): { value: string; label: string }[] {
    const cat = this.filterState.category?.toUpperCase() || 'ALL';
    const all = [
      { value: 'all', label: 'All Actions' },
      { value: 'PASSPORT_VIEWED', label: 'Viewed Business Passport' },
      { value: 'PASSPORT_SHARED', label: 'Shared Sovereign Passport' },
      { value: 'PASSPORT_ACCESS_REVOKED', label: 'Revoked Passport Authorization' },
      { value: 'ONBOARDING_APPROVED', label: 'Approved Supplier' },
      { value: 'ONBOARDING_REJECTED', label: 'Rejected Onboarding' },
      { value: 'ONBOARDING_CREATED', label: 'Initiated Onboarding' },
      { value: 'ONBOARDING_SENT', label: 'Dispatched Invite' },
      { value: 'ONBOARDING_REMINDER_SENT', label: 'Sent Supplier Reminder' },
      { value: 'ONBOARDING_WITHDRAWN', label: 'Withdrew Onboarding' },
      { value: 'SUPPLIER_SHORTLISTED', label: 'Shortlisted Supplier' },
      { value: 'SUPPLIER_SEARCHED', label: 'Executed Directory Search' },
      { value: 'VERIFICATION_REVIEWED', label: 'Reviewed Verification' },
      { value: 'VERIFICATION_FAILED', label: 'Verification Failed' },
      { value: 'CREDENTIAL_VERIFIED', label: 'Verified Credential' },
      { value: 'CREDENTIAL_REJECTED', label: 'Rejected Credential' },
      { value: 'USER_ROLE_CHANGED', label: 'User Role Changed' },
      { value: 'AUDIT_LOG_EXPORTED', label: 'Exported Forensic Ledger' }
    ];

    if (cat === 'all') return all;

    return all.filter(a => {
      if (a.value === 'all') return true;
      if (cat === 'PASSPORT' && a.value.startsWith('PASSPORT')) return true;
      if (cat === 'ONBOARDING' && a.value.startsWith('ONBOARDING')) return true;
      if (cat === 'SUPPLIER' && a.value.startsWith('SUPPLIER')) return true;
      if (cat === 'VERIFICATION' && a.value.startsWith('VERIFICATION')) return true;
      if (cat === 'CREDENTIAL' && a.value.startsWith('CREDENTIAL')) return true;
      if (cat === 'USER' && a.value.startsWith('USER')) return true;
      if (cat === 'SECURITY' && (a.value.includes('EXPORT') || a.value.includes('REVOKED'))) return true;
      return false;
    });
  }

  private resolveDateStrings(): { from?: string; to?: string } {
    if (this.filterState.dateRange === 'custom') {
      return { from: this.filterState.from || undefined, to: this.filterState.to || undefined };
    }
    const now = new Date();
    if (this.filterState.dateRange === 'today') {
      const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
      return { from: today.toISOString() };
    }
    if (this.filterState.dateRange === 'last7') {
      return { from: new Date(now.getTime() - 7 * 86400000).toISOString() };
    }
    if (this.filterState.dateRange === 'last30') {
      return { from: new Date(now.getTime() - 30 * 86400000).toISOString() };
    }
    if (this.filterState.dateRange === 'last90') {
      return { from: new Date(now.getTime() - 90 * 86400000).toISOString() };
    }
    return {};
  }
}
