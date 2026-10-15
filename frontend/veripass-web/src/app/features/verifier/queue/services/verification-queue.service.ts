import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap, catchError, throwError } from 'rxjs';
import { ApiService } from '../../../../core/services/api.service';
import {
  VerificationQueueResponse,
  VerificationQueueFilterParams,
  VerificationQueueSummary
} from '../models/verification-queue.model';

@Injectable({
  providedIn: 'root'
})
export class VerificationQueueService {
  private api = inject(ApiService);

  readonly queueResponse = signal<VerificationQueueResponse | null>(null);
  readonly loading = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly currentParams = signal<VerificationQueueFilterParams>({
    scope: 'all',
    page: 1,
    pageSize: 25,
    status: 'ALL',
    priority: 'ALL',
    type: 'ALL',
    risk: 'ALL',
    dateRange: 'ALL'
  });

  fetchQueue(params: VerificationQueueFilterParams): Observable<VerificationQueueResponse> {
    this.loading.set(true);
    this.error.set(null);
    this.currentParams.set(params);

    const queryParams: Record<string, any> = {
      scope: params.scope || 'all',
      page: params.page || 1,
      pageSize: params.pageSize || 25
    };

    if (params.search && params.search.trim()) {
      queryParams['search'] = params.search.trim();
    }
    if (params.status && params.status !== 'ALL') {
      queryParams['status'] = params.status;
    }
    if (params.priority && params.priority !== 'ALL') {
      queryParams['priority'] = params.priority;
    }
    if (params.type && params.type !== 'ALL') {
      queryParams['type'] = params.type;
    }
    if (params.risk && params.risk !== 'ALL') {
      queryParams['risk'] = params.risk;
    }
    if (params.dateRange && params.dateRange !== 'ALL') {
      queryParams['dateRange'] = params.dateRange;
    }
    if (params.dateFrom) {
      queryParams['dateFrom'] = params.dateFrom;
    }
    if (params.dateTo) {
      queryParams['dateTo'] = params.dateTo;
    }
    if (params.sortBy) {
      queryParams['sortBy'] = params.sortBy;
    }
    if (params.sortDirection) {
      queryParams['sortDirection'] = params.sortDirection;
    }

    return this.api.get<VerificationQueueResponse>('verifications/queue', queryParams).pipe(
      tap({
        next: res => {
          this.queueResponse.set(res);
          this.loading.set(false);
          this.error.set(null);
        },
        error: err => {
          this.loading.set(false);
          const errorMsg = err?.error?.message || 'Unable to connect to verification queue service. Please try again.';
          this.error.set(errorMsg);
        }
      }),
      catchError(err => throwError(() => err))
    );
  }

  fetchSummary(): Observable<VerificationQueueSummary> {
    return this.api.get<VerificationQueueSummary>('verifications/queue/summary');
  }

  formatRelativeTime(isoString: string): string {
    if (!isoString) return '—';
    const submitted = new Date(isoString);
    const now = new Date();
    const diffMs = now.getTime() - submitted.getTime();
    if (diffMs < 0) return 'Just now';

    const diffMinutes = Math.floor(diffMs / 60000);
    if (diffMinutes < 1) return 'Just now';
    if (diffMinutes < 60) return `${diffMinutes} min ago`;

    const diffHours = Math.floor(diffMinutes / 60);
    if (diffHours < 24) {
      const remainingMins = diffMinutes % 60;
      return remainingMins > 0 ? `${diffHours}h ${remainingMins}m ago` : `${diffHours}h ago`;
    }

    const diffDays = Math.floor(diffHours / 24);
    if (diffDays === 1) return '1 day ago';
    if (diffDays < 30) return `${diffDays} days ago`;

    return submitted.toLocaleDateString('en-IN', {
      day: '2-digit',
      month: 'short',
      year: 'numeric'
    });
  }

  formatExactDate(isoString: string): string {
    if (!isoString) return '';
    const date = new Date(isoString);
    return date.toLocaleString('en-IN', {
      day: '2-digit',
      month: 'short',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      timeZoneName: 'short'
    });
  }
}
