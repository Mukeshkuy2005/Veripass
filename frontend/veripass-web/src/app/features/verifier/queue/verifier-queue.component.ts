import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { VerificationQueueService } from './services/verification-queue.service';
import {
  VerificationQueueFilterParams,
  VerificationQueueResponse,
  VerificationQueueSummary
} from './models/verification-queue.model';
import { QueueHeaderComponent } from './components/queue-header/queue-header.component';
import { QueueFiltersComponent } from './components/queue-filters/queue-filters.component';
import { QueueTableComponent } from './components/queue-table/queue-table.component';
import { QueuePaginationComponent } from './components/queue-pagination/queue-pagination.component';

@Component({
  selector: 'app-verifier-queue',
  standalone: true,
  imports: [
    CommonModule,
    QueueHeaderComponent,
    QueueFiltersComponent,
    QueueTableComponent,
    QueuePaginationComponent
  ],
  templateUrl: './verifier-queue.component.html',
  styleUrl: './verifier-queue.component.css'
})
export class VerifierQueueComponent implements OnInit, OnDestroy {
  public queueService = inject(VerificationQueueService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  private queryParamSub?: Subscription;

  // Active filter state
  readonly filterParams = signal<VerificationQueueFilterParams>({
    scope: 'all',
    page: 1,
    pageSize: 25,
    status: 'PENDING',
    priority: 'ALL',
    type: 'ALL',
    risk: 'ALL',
    dateRange: 'ALL',
    sortBy: 'submitted',
    sortDirection: 'desc'
  });

  ngOnInit(): void {
    // 1. Subscribe to URL query parameters to synchronize state
    this.queryParamSub = this.route.queryParams.subscribe(params => {
      const parsed: VerificationQueueFilterParams = {
        scope: params['scope'] === 'my' ? 'my' : 'all',
        search: params['search'] || undefined,
        status: params['status'] !== undefined ? params['status'] : 'PENDING',
        priority: params['priority'] || 'ALL',
        type: params['type'] || 'ALL',
        risk: params['risk'] || 'ALL',
        dateRange: params['dateRange'] || 'ALL',
        dateFrom: params['dateFrom'] || undefined,
        dateTo: params['dateTo'] || undefined,
        page: params['page'] ? Math.max(1, parseInt(params['page'], 10)) : 1,
        pageSize: params['pageSize'] ? parseInt(params['pageSize'], 10) : 25,
        sortBy: params['sortBy'] || 'submitted',
        sortDirection: params['sortDirection'] === 'asc' ? 'asc' : 'desc'
      };

      this.filterParams.set(parsed);
      this.loadQueueData(parsed);
    });
  }

  ngOnDestroy(): void {
    this.queryParamSub?.unsubscribe();
  }

  loadQueueData(params: VerificationQueueFilterParams): void {
    this.queueService.fetchQueue(params).subscribe({
      next: () => {},
      error: () => {}
    });
  }

  onScopeChange(scope: 'my' | 'all'): void {
    this.updateUrlParams({ scope, page: 1 });
  }

  onFilterApply(filters: VerificationQueueFilterParams): void {
    this.updateUrlParams({
      search: filters.search || null,
      status: filters.status,
      priority: filters.priority,
      type: filters.type,
      risk: filters.risk,
      dateRange: filters.dateRange,
      page: 1
    });
  }

  onFilterReset(): void {
    this.updateUrlParams({
      search: null,
      status: 'PENDING',
      priority: 'ALL',
      type: 'ALL',
      risk: 'ALL',
      dateRange: 'ALL',
      page: 1
    });
  }

  onSortChange(event: { sortBy: string; sortDirection: 'asc' | 'desc' }): void {
    this.updateUrlParams({
      sortBy: event.sortBy,
      sortDirection: event.sortDirection,
      page: 1
    });
  }

  onPageChange(page: number): void {
    this.updateUrlParams({ page });
  }

  onPageSizeChange(pageSize: number): void {
    this.updateUrlParams({ pageSize, page: 1 });
  }

  onRefresh(): void {
    this.loadQueueData(this.filterParams());
  }

  private updateUrlParams(newParams: Partial<Record<string, any>>): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: newParams,
      queryParamsHandling: 'merge'
    });
  }
}
