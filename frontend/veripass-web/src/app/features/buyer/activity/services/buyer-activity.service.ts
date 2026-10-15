import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from '../../../../core/services/api.service';
import {
  BuyerActivityDetail,
  BuyerActivityFilterState,
  BuyerActivityPagedResult,
  BuyerActivitySummary,
  BuyerActivityUser,
  ExportBuyerActivityRequest
} from '../models/buyer-activity.model';

@Injectable({
  providedIn: 'root'
})
export class BuyerActivityService {
  private api = inject(ApiService);
  private http = inject(HttpClient);
  private readonly baseUrl = 'http://localhost:5000/api/v1/buyer/activity';

  /**
   * Fetches paginated, filtered audit logs from the backend.
   */
  getActivity(filter: BuyerActivityFilterState): Observable<BuyerActivityPagedResult> {
    const params: Record<string, any> = {
      page: filter.page,
      pageSize: filter.pageSize,
      sort: filter.sort || 'timestamp_desc'
    };

    if (filter.search && filter.search.trim()) {
      params['search'] = filter.search.trim();
    }

    // Resolve date range to ISO strings
    const { from, to } = this.resolveDateRange(filter);
    if (from) params['from'] = from;
    if (to) params['to'] = to;

    if (filter.userId && filter.userId !== 'all') {
      params['userId'] = filter.userId;
    }

    if (filter.category && filter.category !== 'all') {
      params['category'] = filter.category;
    }

    if (filter.action && filter.action !== 'all') {
      params['action'] = filter.action;
    }

    if (filter.target && filter.target.trim() && filter.target !== 'all') {
      params['target'] = filter.target.trim();
    }

    if (filter.result && filter.result !== 'all') {
      params['result'] = filter.result;
    }

    return this.api.get<BuyerActivityPagedResult>('buyer/activity', params);
  }

  /**
   * Fetches operational summary statistics (Today, Success, Failed, Denied, Total).
   */
  getSummary(): Observable<BuyerActivitySummary> {
    return this.api.get<BuyerActivitySummary>('buyer/activity/summary');
  }

  /**
   * Fetches list of users who have conducted operations in the organization.
   */
  getUsers(): Observable<BuyerActivityUser[]> {
    return this.api.get<BuyerActivityUser[]>('buyer/activity/users');
  }

  /**
   * Fetches comprehensive audit forensics for a single event.
   */
  getDetail(id: string): Observable<BuyerActivityDetail> {
    return this.api.get<BuyerActivityDetail>(`buyer/activity/${id}`);
  }

  /**
   * Downloads real exported audit ledger (CSV or JSON).
   */
  exportActivity(request: ExportBuyerActivityRequest): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/export`, request, {
      responseType: 'blob'
    });
  }

  private resolveDateRange(filter: BuyerActivityFilterState): { from?: string; to?: string } {
    if (filter.dateRange === 'custom') {
      return {
        from: filter.from || undefined,
        to: filter.to || undefined
      };
    }

    const now = new Date();
    if (filter.dateRange === 'today') {
      const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
      return { from: today.toISOString() };
    }

    if (filter.dateRange === 'last7') {
      const past = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
      return { from: past.toISOString() };
    }

    if (filter.dateRange === 'last30') {
      const past = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);
      return { from: past.toISOString() };
    }

    if (filter.dateRange === 'last90') {
      const past = new Date(now.getTime() - 90 * 24 * 60 * 60 * 1000);
      return { from: past.toISOString() };
    }

    return {};
  }
}
