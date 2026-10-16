import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { ApiService } from './api.service';

export interface EscalatedCaseItem {
  caseId: string;
  caseGuid: string;
  businessId: string;
  businessName: string;
  gtin?: string;
  gstin?: string;
  pan?: string;
  businessIdNumber?: string;
  verificationType: string;
  escalationReason: string;
  priority: string;
  riskLevel: string;
  riskScore: number;
  escalatedAtUtc: string;
  escalatedByName?: string;
  assignedVerifierId?: string;
  assignedVerifierName?: string;
  status: string;
  concurrencyToken: number;
}

export interface EscalatedCasesSummary {
  totalCount: number;
  assignedCount: number;
  unassignedCount: number;
}

export interface EscalatedCasesResponse {
  items: EscalatedCaseItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  summary: EscalatedCasesSummary;
  serverTimeUtc: string;
}

export interface EscalatedEvidenceItem {
  evidenceId: string;
  documentId?: string;
  documentType: string;
  fileName: string;
  status: string;
  uploadedAtUtc: string;
}

export interface EscalationPreviousReview {
  verifierName: string;
  decision: string;
  attemptCount: number;
  lastReviewedAtUtc?: string;
}

export interface EscalatedCaseDetail {
  caseId: string;
  caseGuid: string;
  businessId: string;
  businessName: string;
  gstin?: string;
  pan?: string;
  businessIdNumber?: string;
  verificationType: string;
  escalationReason: string;
  priority: string;
  riskLevel: string;
  riskScore?: number;
  trustScore?: number;
  escalatedAtUtc: string;
  escalatedByName: string;
  assignedVerifierId?: string;
  assignedVerifierName?: string;
  status: string;
  concurrencyToken: number;
  evidences: EscalatedEvidenceItem[];
  previousReview: EscalationPreviousReview;
  canSeniorReview: boolean;
  canClaim: boolean;
}

export interface EscalatedCasesQueryParams {
  view?: 'all' | 'mine' | 'unassigned';
  search?: string;
  priority?: string;
  reason?: string;
  assignedTo?: string;
  dateFrom?: string;
  dateTo?: string;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: string;
}

@Injectable({
  providedIn: 'root'
})
export class VerifierEscalatedService {
  private api = inject(ApiService);

  getEscalatedCases(params: EscalatedCasesQueryParams): Observable<EscalatedCasesResponse> {
    const queryParams: Record<string, any> = {};
    if (params.view) queryParams['view'] = params.view;
    if (params.search && params.search.trim()) queryParams['search'] = params.search.trim();
    if (params.priority && params.priority !== 'ALL') queryParams['priority'] = params.priority;
    if (params.reason && params.reason !== 'ALL') queryParams['reason'] = params.reason;
    if (params.assignedTo && params.assignedTo !== 'ALL') queryParams['assignedTo'] = params.assignedTo;
    if (params.dateFrom) queryParams['dateFrom'] = params.dateFrom;
    if (params.dateTo) queryParams['dateTo'] = params.dateTo;
    if (params.page) queryParams['page'] = params.page;
    if (params.pageSize) queryParams['pageSize'] = params.pageSize;
    if (params.sortBy) queryParams['sortBy'] = params.sortBy;
    if (params.sortDirection) queryParams['sortDirection'] = params.sortDirection;

    return this.api.get<any>('verifier/escalated', queryParams).pipe(
      map(res => (res && res.data ? res.data : res) as EscalatedCasesResponse)
    );
  }

  getEscalationDetail(caseId: string): Observable<EscalatedCaseDetail> {
    return this.api.get<any>(`verifier/escalated/${encodeURIComponent(caseId)}`).pipe(
      map(res => (res && res.data ? res.data : res) as EscalatedCaseDetail)
    );
  }

  claimCase(caseId: string, concurrencyToken?: number): Observable<{ success: boolean }> {
    return this.api.post<{ success: boolean }>(`verifier/escalated/${encodeURIComponent(caseId)}/claim`, {
      concurrencyToken
    });
  }

  approveCase(caseId: string, notes?: string, concurrencyToken?: number): Observable<{ success: boolean }> {
    return this.api.post<{ success: boolean }>(`verifier/escalated/${encodeURIComponent(caseId)}/approve`, {
      notes,
      concurrencyToken
    });
  }

  rejectCase(caseId: string, reason: string, concurrencyToken?: number): Observable<{ success: boolean }> {
    return this.api.post<{ success: boolean }>(`verifier/escalated/${encodeURIComponent(caseId)}/reject`, {
      reason,
      concurrencyToken
    });
  }

  returnCase(caseId: string, reason: string, concurrencyToken?: number): Observable<{ success: boolean }> {
    return this.api.post<{ success: boolean }>(`verifier/escalated/${encodeURIComponent(caseId)}/return`, {
      reason,
      concurrencyToken
    });
  }
}
