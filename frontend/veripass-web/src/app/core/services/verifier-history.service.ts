import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { ApiService } from './api.service';

export interface VerifierHistoryItem {
  eventId: string;
  caseId: string;
  caseGuid: string;
  businessId?: string;
  businessName: string;
  supplierIdentifier?: string;
  verificationType: string;
  decision: string;
  verifierId?: string;
  verifierName: string;
  verifierRole: string;
  decisionAtUtc: string;
  previousState?: string;
  newState?: string;
  decisionReason?: string;
  priority: string;
  riskLevel: string;
  riskScore: number;
  evidenceExamined: string[];
}

export interface VerifierHistoryResponse {
  items: VerifierHistoryItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  serverTimeUtc: string;
}

export interface CaseTimelineHistoryEvent {
  eventId: string;
  action: string;
  actorName: string;
  timestamp: string;
  result: string;
  reason?: string;
}

export interface VerifierHistoryDetail {
  eventId: string;
  caseId: string;
  caseGuid: string;
  businessId?: string;
  businessName: string;
  supplierIdentifier?: string;
  verificationType: string;
  decision: string;
  verifierId?: string;
  verifierName: string;
  verifierRole: string;
  decisionAtUtc: string;
  previousState?: string;
  newState?: string;
  decisionReason?: string;
  priority: string;
  riskLevel: string;
  riskScore: number;
  evidenceExamined: string[];
  timelineChanges: CaseTimelineHistoryEvent[];
}

export interface VerifierFilterOptionItem {
  value: string;
  label: string;
}

export interface VerifierOption {
  id: string;
  name: string;
  role: string;
}

export interface VerifierHistoryFilterOptions {
  decisions: VerifierFilterOptionItem[];
  verificationTypes: string[];
  verifiers: VerifierOption[];
  dateRanges: VerifierFilterOptionItem[];
}

export interface HistoryQueryParams {
  from?: string;
  to?: string;
  decision?: string;
  verificationType?: string;
  verifierId?: string;
  search?: string;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: string;
}

@Injectable({
  providedIn: 'root'
})
export class VerifierHistoryService {
  private api = inject(ApiService);

  getHistory(params: HistoryQueryParams): Observable<VerifierHistoryResponse> {
    const queryParams: Record<string, any> = {};
    if (params.from) queryParams['from'] = params.from;
    if (params.to) queryParams['to'] = params.to;
    if (params.decision && params.decision !== 'ALL') queryParams['decision'] = params.decision;
    if (params.verificationType && params.verificationType !== 'ALL') queryParams['verificationType'] = params.verificationType;
    if (params.verifierId && params.verifierId !== 'ALL') queryParams['verifierId'] = params.verifierId;
    if (params.search && params.search.trim()) queryParams['search'] = params.search.trim();
    if (params.page) queryParams['page'] = params.page;
    if (params.pageSize) queryParams['pageSize'] = params.pageSize;
    if (params.sortBy) queryParams['sortBy'] = params.sortBy;
    if (params.sortDirection) queryParams['sortDirection'] = params.sortDirection;

    return this.api.get<any>('verifier/history', queryParams).pipe(
      map(res => (res && res.data ? res.data : res) as VerifierHistoryResponse)
    );
  }

  getHistoryDetail(eventId: string): Observable<VerifierHistoryDetail> {
    return this.api.get<any>(`verifier/history/${encodeURIComponent(eventId)}`).pipe(
      map(res => (res && res.data ? res.data : res) as VerifierHistoryDetail)
    );
  }

  getFilterOptions(): Observable<VerifierHistoryFilterOptions> {
    return this.api.get<any>('verifier/history/filters').pipe(
      map(res => (res && res.data ? res.data : res) as VerifierHistoryFilterOptions)
    );
  }
}
