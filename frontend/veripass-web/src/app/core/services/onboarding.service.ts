import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import {
  OnboardingPagedList,
  OnboardingRequestDetail,
  SupplierSearchItem,
  RequirementCatalogueItem,
  CreateOnboardingRequestPayload,
  ApproveOnboardingPayload,
  RejectOnboardingPayload
} from '../models/models';

@Injectable({
  providedIn: 'root'
})
export class OnboardingService {
  private api = inject(ApiService);

  /**
   * Retrieves paginated, filtered onboarding requests with aggregate status counts.
   */
  getOnboardingRequests(params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    status?: string;
    category?: string;
    requestedBy?: string;
    sortBy?: string;
    sortDirection?: string;
  }): Observable<OnboardingPagedList> {
    return this.api.get<OnboardingPagedList>('buyer/onboarding', params);
  }

  /**
   * Retrieves the full catalogue of available compliance and statutory onboarding requirements.
   */
  getRequirementsCatalogue(): Observable<RequirementCatalogueItem[]> {
    return this.api.get<RequirementCatalogueItem[]>('buyer/onboarding/requirements');
  }

  /**
   * Searches sovereign businesses eligible for buyer onboarding.
   */
  searchSuppliers(query?: string): Observable<SupplierSearchItem[]> {
    const params = query ? { q: query } : undefined;
    return this.api.get<SupplierSearchItem[]>('buyer/onboarding/suppliers/search', params);
  }

  /**
   * Retrieves a single onboarding request detail by ID with complete verification proofs and audit activity.
   */
  getOnboardingRequest(id: string): Observable<OnboardingRequestDetail> {
    return this.api.get<OnboardingRequestDetail>(`buyer/onboarding/${id}`);
  }

  /**
   * Creates a new onboarding request with buyer-selected requirements.
   */
  createOnboardingRequest(payload: CreateOnboardingRequestPayload): Observable<{ id: string; message: string }> {
    return this.api.post<{ id: string; message: string }>('buyer/onboarding', payload);
  }

  /**
   * Dispatches an onboarding request to the supplier if not already sent.
   */
  sendRequest(id: string): Observable<{ success: boolean }> {
    return this.api.post<{ success: boolean }>(`buyer/onboarding/${id}/send`, {});
  }

  /**
   * Approves a verified onboarding request for ERP vendor master creation.
   */
  approve(id: string, payload?: ApproveOnboardingPayload): Observable<{ success: boolean; message: string }> {
    return this.api.post<{ success: boolean; message: string }>(`buyer/onboarding/${id}/approve`, payload || {});
  }

  /**
   * Rejects an onboarding request with official procurement justification.
   */
  reject(id: string, payload: RejectOnboardingPayload): Observable<{ success: boolean; message: string }> {
    return this.api.post<{ success: boolean; message: string }>(`buyer/onboarding/${id}/reject`, payload);
  }

  /**
   * Withdraws an active onboarding request.
   */
  withdraw(id: string): Observable<{ success: boolean; message: string }> {
    return this.api.post<{ success: boolean; message: string }>(`buyer/onboarding/${id}/withdraw`, {});
  }

  /**
   * Sends an automated reminder notification to the supplier.
   */
  remind(id: string): Observable<{ success: boolean; message: string }> {
    return this.api.post<{ success: boolean; message: string }>(`buyer/onboarding/${id}/remind`, {});
  }
}
