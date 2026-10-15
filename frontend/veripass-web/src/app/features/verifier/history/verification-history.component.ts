import { Component, OnInit, OnDestroy, inject, signal, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  VerifierHistoryService,
  VerifierHistoryItem,
  VerifierHistoryDetail,
  VerifierHistoryFilterOptions,
  HistoryQueryParams
} from '../../../core/services/verifier-history.service';
import {
  formatIstDateTime,
  formatRelativeTime,
  FormattedIstDateTime
} from '../../../core/utils/ist-date-formatter';

@Component({
  selector: 'vp-verification-history',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './verification-history.component.html',
  styleUrl: './verification-history.component.css'
})
export class VerificationHistoryComponent implements OnInit, OnDestroy {
  private historyService = inject(VerifierHistoryService);

  // Loading & Error States
  loading = signal<boolean>(true);
  error = signal<string | null>(null);

  // Table Data & Pagination
  items = signal<VerifierHistoryItem[]>([]);
  totalCount = signal<number>(0);
  totalPages = signal<number>(1);
  page = signal<number>(1);
  pageSize = signal<number>(25);
  sortBy = signal<string>('decisionAt');
  sortDirection = signal<'asc' | 'desc'>('desc');

  // Filter States
  selectedDateRange = signal<string>('LAST_30_DAYS');
  customFrom = signal<string>('');
  customTo = signal<string>('');
  selectedDecision = signal<string>('ALL');
  selectedType = signal<string>('ALL');
  selectedVerifier = signal<string>('ALL');
  searchQuery = signal<string>('');

  // Available Filter Options from Backend
  filterOptions = signal<VerifierHistoryFilterOptions>({
    decisions: [
      { value: 'ALL', label: 'All Decisions' },
      { value: 'APPROVED', label: 'Approved' },
      { value: 'REJECTED', label: 'Rejected' },
      { value: 'MORE_INFO', label: 'More Info' },
      { value: 'ESCALATED', label: 'Escalated' }
    ],
    verificationTypes: ['GST', 'PAN', 'Bank', 'Address', 'ISO'],
    verifiers: [],
    dateRanges: [
      { value: 'LAST_30_DAYS', label: 'Last 30 days' },
      { value: 'TODAY', label: 'Today' },
      { value: 'LAST_7_DAYS', label: 'Last 7 days' },
      { value: 'LAST_90_DAYS', label: 'Last 90 days' },
      { value: 'ALL_TIME', label: 'All time' },
      { value: 'CUSTOM', label: 'Custom range' }
    ]
  });

  // Drawer / Detail State
  selectedRow = signal<VerifierHistoryItem | null>(null);
  detailLoading = signal<boolean>(false);
  detailData = signal<VerifierHistoryDetail | null>(null);
  detailError = signal<string | null>(null);

  // Live Relative Time Reference Ticker (updates every 30 seconds)
  currentTime = signal<Date>(new Date());
  private timeTickerInterval: any = null;

  // Real Last Updated Timestamp in IST
  lastUpdatedText = signal<string>('—');

  ngOnInit(): void {
    this.loadFilterOptions();
    this.loadHistory();

    // Live clock ticker to update relative time without reloading the page
    this.timeTickerInterval = setInterval(() => {
      this.currentTime.set(new Date());
    }, 30000);
  }

  ngOnDestroy(): void {
    if (this.timeTickerInterval) {
      clearInterval(this.timeTickerInterval);
      this.timeTickerInterval = null;
    }
  }

  @HostListener('window:keydown.escape')
  handleEscape(): void {
    if (this.selectedRow()) {
      this.closeDrawer();
    }
  }

  loadFilterOptions(): void {
    this.historyService.getFilterOptions().subscribe({
      next: (opts) => {
        if (opts) {
          this.filterOptions.set(opts);
        }
      },
      error: () => {
        // Fallback options already present
      }
    });
  }

  loadHistory(): void {
    this.loading.set(true);
    this.error.set(null);

    const params: HistoryQueryParams = {
      page: this.page(),
      pageSize: this.pageSize(),
      sortBy: this.sortBy(),
      sortDirection: this.sortDirection()
    };

    if (this.selectedDecision() !== 'ALL') {
      params.decision = this.selectedDecision();
    }

    if (this.selectedType() !== 'ALL') {
      params.verificationType = this.selectedType();
    }

    if (this.selectedVerifier() !== 'ALL') {
      params.verifierId = this.selectedVerifier();
    }

    if (this.searchQuery().trim()) {
      params.search = this.searchQuery().trim();
    }

    // Date Range calculation
    const now = new Date();
    const range = this.selectedDateRange();

    if (range === 'TODAY') {
      const todayStart = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate(), 0, 0, 0));
      params.from = todayStart.toISOString();
    } else if (range === 'LAST_7_DAYS') {
      const start = new Date(now.getTime() - 7 * 24 * 3600 * 1000);
      params.from = start.toISOString();
    } else if (range === 'LAST_30_DAYS') {
      const start = new Date(now.getTime() - 30 * 24 * 3600 * 1000);
      params.from = start.toISOString();
    } else if (range === 'LAST_90_DAYS') {
      const start = new Date(now.getTime() - 90 * 24 * 3600 * 1000);
      params.from = start.toISOString();
    } else if (range === 'CUSTOM') {
      if (this.customFrom()) {
        params.from = new Date(this.customFrom()).toISOString();
      }
      if (this.customTo()) {
        const toDate = new Date(this.customTo());
        toDate.setHours(23, 59, 59, 999);
        params.to = toDate.toISOString();
      }
    }

    this.historyService.getHistory(params).subscribe({
      next: (res) => {
        this.items.set(res.items || []);
        this.totalCount.set(res.totalCount || 0);
        this.totalPages.set(res.totalPages || 1);
        this.loading.set(false);

        // Update real "Last updated" time
        const refreshIst = formatIstDateTime(new Date());
        this.lastUpdatedText.set(`${refreshIst.timePart}`);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('We could not load verification history from the server.');
      }
    });
  }

  applyFilters(): void {
    this.page.set(1);
    this.loadHistory();
  }

  resetFilters(): void {
    this.selectedDateRange.set('LAST_30_DAYS');
    this.customFrom.set('');
    this.customTo.set('');
    this.selectedDecision.set('ALL');
    this.selectedType.set('ALL');
    this.selectedVerifier.set('ALL');
    this.searchQuery.set('');
    this.page.set(1);
    this.loadHistory();
  }

  onSort(field: string): void {
    if (this.sortBy() === field) {
      this.sortDirection.set(this.sortDirection() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortBy.set(field);
      this.sortDirection.set('desc');
    }
    this.page.set(1);
    this.loadHistory();
  }

  goToPage(p: number): void {
    if (p >= 1 && p <= this.totalPages() && p !== this.page()) {
      this.page.set(p);
      this.loadHistory();
    }
  }

  onPageSizeChange(newSize: number): void {
    this.pageSize.set(newSize);
    this.page.set(1);
    this.loadHistory();
  }

  selectRow(item: VerifierHistoryItem): void {
    this.selectedRow.set(item);
    this.detailLoading.set(true);
    this.detailError.set(null);
    this.detailData.set(null);

    const eventIdToQuery = item.eventId || item.caseId;

    this.historyService.getHistoryDetail(eventIdToQuery).subscribe({
      next: (detail) => {
        this.detailData.set(detail);
        this.detailLoading.set(false);
      },
      error: () => {
        this.detailLoading.set(false);
        this.detailError.set('Could not load detailed event records.');
      }
    });
  }

  closeDrawer(): void {
    this.selectedRow.set(null);
    this.detailData.set(null);
    this.detailError.set(null);
  }

  // Timezone and relative date presentation helpers
  formatDate(utcTimestamp: string | Date | null | undefined): FormattedIstDateTime {
    return formatIstDateTime(utcTimestamp);
  }

  formatRelative(utcTimestamp: string | Date | null | undefined): string {
    return formatRelativeTime(utcTimestamp, this.currentTime());
  }

  getPaginationRange(): number[] {
    const current = this.page();
    const total = this.totalPages();
    const delta = 2;
    const range: number[] = [];

    for (let i = Math.max(1, current - delta); i <= Math.min(total, current + delta); i++) {
      range.push(i);
    }
    return range;
  }
}
