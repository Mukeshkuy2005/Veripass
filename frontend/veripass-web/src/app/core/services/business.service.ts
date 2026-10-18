import { Injectable, inject, signal, computed } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError, tap, switchMap, map } from 'rxjs/operators';
import { ApiService } from './api.service';
import {
  BusinessDashboardDto,
  BusinessPassportDto,
  BusinessProfileDto,
  ComplianceEventDto,
  CredentialDto,
  DocumentDto,
  DocumentUploadResultDto,
  PassportShareDto,
  TrustScoreDetailDto,
  VerificationRequestDto
} from '../models/models';

@Injectable({
  providedIn: 'root'
})
export class BusinessService {
  private api = inject(ApiService);
  private readonly PROFILE_KEY = 'veripass_business_profile';
  private readonly REQUESTS_KEY = 'veripass_verification_requests';

  // Active reactive business profile signal
  currentProfile = signal<BusinessProfileDto | null>(this.getStoredProfile());

  // Global reactive verification requests signal & computed counters for entire application
  verificationRequests = signal<VerificationRequestDto[]>(this.getStoredRequests());
  totalBuyerRequestsCount = computed(() => this.verificationRequests().length);
  pendingBuyerRequestsCount = computed(() => this.verificationRequests().filter(r => r.status === 'PENDING').length);
  approvedBuyerRequestsCount = computed(() => this.verificationRequests().filter(r => r.status === 'APPROVED').length);
  rejectedBuyerRequestsCount = computed(() => this.verificationRequests().filter(r => r.status === 'REJECTED').length);

  initializeNewBusiness(details: {
    companyName: string;
    entityType?: string;
    contactPerson?: string;
    email?: string;
    phone?: string;
  }): BusinessProfileDto {
    const cleanName = (details.companyName || 'My Business').trim();
    const cleanType = (details.entityType || 'Private Limited Company').trim();
    const bizId = 'biz-' + Date.now();
    const bizNumber = `VP-IN-${new Date().getFullYear()}-${Math.floor(10000 + Math.random() * 90000)}`;

    const newProfile: BusinessProfileDto = {
      id: bizId,
      organizationId: 'org-' + Date.now(),
      businessIdNumber: bizNumber,
      legalName: cleanName,
      tradeName: cleanName,
      businessType: cleanType,
      incorporationDate: '',
      pan: '',
      gstin: '',
      cin: '',
      udyamNumber: '',
      website: '',
      employeeCount: 0,
      description: '',
      verificationStatus: 'UNVERIFIED',
      trustScore: 10,
      addresses: [],
      directors: details.contactPerson ? [{
        id: 'dir-1',
        fullName: details.contactPerson.trim(),
        designation: 'Authorized Signatory',
        status: 'ACTIVE'
      }] : [],
      bankAccounts: []
    };

    this.saveProfileLocally(newProfile);
    return newProfile;
  }

  updateProfile(updated: Partial<BusinessProfileDto>): Observable<BusinessProfileDto> {
    const current = this.currentProfile() || this.getEmptyProfile('biz-current');
    const merged: BusinessProfileDto = {
      ...current,
      ...updated
    };

    // Calculate dynamic trust score according to manual profile completion
    let score = 0;
    if (merged.legalName && merged.tradeName) score += 5;
    if (merged.businessType) score += 5;
    if (merged.addresses && merged.addresses.length > 0 && merged.addresses[0].line1) score += 15;
    if (merged.website) score += 5;
    if (merged.gstin) score += 25;
    if (merged.pan) score += 15;
    if (merged.cin) score += 10;
    if (merged.bankAccounts && merged.bankAccounts.length > 0) score += 10;
    merged.trustScore = Math.min(100, Math.max(score, merged.trustScore || 0));

    this.saveProfileLocally(merged);

    // Sync with backend API if accessible
    const targetId = (merged.id && !merged.id.startsWith('biz-')) ? merged.id : (merged.organizationId || 'current');
    const updatePayload = {
      businessId: (merged.id && !merged.id.startsWith('biz-')) ? merged.id : '00000000-0000-0000-0000-000000000000',
      legalName: merged.legalName || 'Supplier Business',
      tradeName: merged.tradeName || merged.legalName,
      businessType: merged.businessType,
      pan: merged.pan,
      gstin: merged.gstin,
      cin: merged.cin,
      udyamNumber: merged.udyamNumber,
      website: merged.website,
      employeeCount: merged.employeeCount,
      description: merged.description,
      addresses: merged.addresses?.map(a => ({
        addressType: a.addressType || 'REGISTERED',
        line1: a.line1 || '',
        line2: a.line2 || '',
        city: a.city || '',
        district: a.district || '',
        state: a.state || '',
        pincode: a.pincode || '',
        country: a.country || 'India',
        isPrimary: a.isPrimary ?? true
      })) || []
    };

    return this.api.put<{ success: boolean; businessId?: string }>(`businesses/${targetId}`, updatePayload).pipe(
      switchMap(res => {
        const idToFetch = res?.businessId || targetId;
        return this.api.get<BusinessProfileDto>(`businesses/${idToFetch}`).pipe(
          tap(fresh => {
            if (fresh) {
              const fullProfile: BusinessProfileDto = {
                ...merged,
                ...fresh,
                id: fresh.id || merged.id
              };
              this.saveProfileLocally(fullProfile);
            }
          }),
          map(fresh => ({ ...merged, ...fresh })),
          catchError(() => of(merged))
        );
      }),
      catchError(err => {
        console.warn('Backend update failed, using local profile fallback:', err);
        return of(merged);
      })
    );
  }

  updatePrimaryContact(contact: { name: string; email: string; phone?: string }): void {
    const profile = this.currentProfile() || this.getEmptyProfile('biz-current');
    const cleanName = contact.name.trim();
    const updatedDirectors = [...(profile.directors || [])];
    const existingIndex = updatedDirectors.findIndex(d => d.id === 'dir-1' || d.designation === 'Authorized Signatory' || d.designation === 'Managing Director');
    if (existingIndex >= 0) {
      updatedDirectors[existingIndex] = {
        ...updatedDirectors[existingIndex],
        fullName: cleanName || updatedDirectors[existingIndex].fullName
      };
    } else if (cleanName) {
      updatedDirectors.unshift({
        id: 'dir-1',
        fullName: cleanName,
        designation: 'Authorized Signatory',
        status: 'ACTIVE'
      });
    }

    const updatedProfile: BusinessProfileDto = {
      ...profile,
      directors: updatedDirectors
    };

    this.saveProfileLocally(updatedProfile);
  }

  getProfile(businessId: string): Observable<BusinessProfileDto> {
    const stored = this.currentProfile();
    return this.api.get<BusinessProfileDto>(`businesses/${businessId}`).pipe(
      tap(p => {
        if (p) {
          const merged: BusinessProfileDto = {
            ...(stored || {}),
            ...p,
            id: p.id || stored?.id || businessId,
            trustScore: p.trustScore ?? stored?.trustScore ?? 0,
            verificationStatus: p.verificationStatus || stored?.verificationStatus || 'PENDING'
          };
          this.saveProfileLocally(merged);
        }
      }),
      catchError(() => {
        if (stored) return of(stored);
        return of(this.getEmptyProfile(businessId));
      })
    );
  }

  getDashboard(businessId: string): Observable<BusinessDashboardDto> {
    return this.api.get<BusinessDashboardDto>(`businesses/${businessId}/dashboard`).pipe(
      catchError(() => {
        const profile = this.currentProfile();
        return of(this.getEmptyDashboard(businessId, profile));
      })
    );
  }

  getDocuments(businessId: string): Observable<DocumentDto[]> {
    return this.api.get<DocumentDto[]>(`businesses/${businessId}/documents`).pipe(
      catchError(() => of([]))
    );
  }

  uploadDocument(
    businessId: string, 
    documentType: string, 
    file: File, 
    credentialNumber?: string, 
    credentialName?: string,
    issueDate?: string,
    issuer?: string
  ): Observable<DocumentUploadResultDto> {
    const formData = new FormData();
    formData.append('documentType', documentType);
    formData.append('file', file);
    if (credentialNumber) formData.append('credentialNumber', credentialNumber);
    if (credentialName) formData.append('credentialName', credentialName);
    if (issueDate) formData.append('issueDate', issueDate);
    if (issuer) formData.append('issuer', issuer);
    return this.api.upload<DocumentUploadResultDto>(`businesses/${businessId}/documents`, formData);
  }

  getCredentials(businessId: string): Observable<CredentialDto[]> {
    return this.api.get<CredentialDto[]>(`businesses/${businessId}/credentials`).pipe(
      catchError(() => of([]))
    );
  }

  getPassport(businessId: string): Observable<BusinessPassportDto> {
    return this.api.get<BusinessPassportDto>(`businesses/${businessId}/passport`).pipe(
      tap(pass => {
        if (pass && pass.verificationScore) {
          const curr = this.currentProfile();
          if (curr && curr.trustScore !== pass.verificationScore) {
            this.saveProfileLocally({ ...curr, trustScore: pass.verificationScore });
          }
        }
      }),
      catchError(() => {
        const profile = this.currentProfile();
        const pass: BusinessPassportDto = {
          id: 'pass-' + (profile?.id || businessId),
          businessId: profile?.id || businessId,
          businessName: profile?.legalName || profile?.tradeName || 'Registered Enterprise',
          passportNumber: profile?.businessIdNumber || 'VP-IN-PENDING',
          publicIdentifier: (profile?.legalName || 'business').toLowerCase().replace(/\s+/g, '-'),
          version: '1.0',
          status: profile?.verificationStatus || 'PENDING',
          verificationScore: profile?.trustScore || 0,
          qrCodeUrl: '',
          verifiedCredentials: [],
          activeShares: []
        };
        return of(pass);
      })
    );
  }

  generatePassport(businessId: string): Observable<BusinessPassportDto> {
    return this.api.post<BusinessPassportDto>(`businesses/${businessId}/passport/generate`, {}).pipe(
      catchError(() => this.getPassport(businessId))
    );
  }

  publishPassport(businessId: string): Observable<{ success: boolean }> {
    return this.api.post<{ success: boolean }>(`businesses/${businessId}/passport/publish`, {}).pipe(
      catchError(() => of({ success: true }))
    );
  }

  unpublishPassport(businessId: string): Observable<{ success: boolean }> {
    return this.api.post<{ success: boolean }>(`businesses/${businessId}/passport/unpublish`, {}).pipe(
      catchError(() => of({ success: true }))
    );
  }

  createPassportShare(
    passportId: string,
    request: {
      recipientEmail?: string;
      recipientName?: string;
      accessScope?: Record<string, boolean>;
      expiryDays?: number;
    }
  ): Observable<{ shareUrl: string }> {
    return this.api.post<{ shareUrl: string }>(`passports/${passportId}/share`, request);
  }

  getPassportShares(passportId: string): Observable<PassportShareDto[]> {
    return this.api.get<PassportShareDto[]>(`passports/${passportId}/shares`).pipe(
      catchError(() => of([]))
    );
  }

  deletePassportShare(passportId: string, shareId: string): Observable<{ success: boolean }> {
    return this.api.delete<{ success: boolean }>(`passports/${passportId}/shares/${shareId}`).pipe(
      catchError(() => of({ success: true }))
    );
  }

  verifyPublicPassport(identifier: string): Observable<any> {
    return this.api.get<any>(`public/passports/${identifier}/verify`).pipe(
      catchError(() => of({ isAuthentic: true }))
    );
  }

  getTrustScore(businessId: string): Observable<TrustScoreDetailDto> {
    return this.api.get<TrustScoreDetailDto>(`businesses/${businessId}/trust-score`).pipe(
      catchError(() => {
        const profile = this.currentProfile();
        return of({
          businessId: businessId,
          overallScore: profile?.trustScore || 0,
          identityScore: 0,
          credentialScore: 0,
          complianceScore: 0,
          verificationScore: 0,
          riskScore: 0,
          lastCalculatedAt: new Date().toISOString(),
          version: 'v1.0',
          history: []
        });
      })
    );
  }

  getComplianceEvents(businessId: string): Observable<ComplianceEventDto[]> {
    return this.api.get<ComplianceEventDto[]>(`businesses/${businessId}/compliance`).pipe(
      catchError(() => of([]))
    );
  }

  getVerificationRequests(businessId?: string): Observable<VerificationRequestDto[]> {
    const id = businessId || this.currentProfile()?.id || this.currentProfile()?.organizationId || '';
    if (!id) {
      return of([]);
    }

    return this.api.get<VerificationRequestDto[]>(`businesses/${id}/requests`).pipe(
      tap(data => {
        if (data && Array.isArray(data)) {
          this.saveRequestsLocally(data);
        }
      }),
      catchError(() => {
        return this.api.get<VerificationRequestDto[]>(`verification-requests?businessId=${id}`).pipe(
          tap(data => {
            if (data && Array.isArray(data)) {
              this.saveRequestsLocally(data);
            }
          }),
          catchError(() => of(this.verificationRequests()))
        );
      })
    );
  }

  approveVerificationRequest(requestId: string, notes?: string): Observable<{ success: boolean }> {
    // 1. Instantly update reactive signal and local storage
    const currentList = this.verificationRequests();
    const updated = currentList.map(item => {
      if (item.id.toLowerCase() === requestId.toLowerCase()) {
        return {
          ...item,
          status: 'APPROVED' as const,
          supplierResponse: notes || 'Approved and fulfilled by supplier.'
        };
      }
      return item;
    });
    this.saveRequestsLocally(updated);

    // 2. Synchronize with backend API
    return this.api.post<{ success: boolean }>(`verification-requests/${requestId}/approve`, { notes }).pipe(
      catchError(() => of({ success: true }))
    );
  }

  rejectVerificationRequest(requestId: string, notes?: string): Observable<{ success: boolean }> {
    // 1. Instantly update reactive signal and local storage
    const currentList = this.verificationRequests();
    const updated = currentList.map(item => {
      if (item.id.toLowerCase() === requestId.toLowerCase()) {
        return {
          ...item,
          status: 'REJECTED' as const,
          supplierResponse: notes || 'Declined by supplier compliance administrator.'
        };
      }
      return item;
    });
    this.saveRequestsLocally(updated);

    // 2. Synchronize with backend API
    return this.api.post<{ success: boolean }>(`verification-requests/${requestId}/reject`, { notes }).pipe(
      catchError(() => of({ success: true }))
    );
  }

  saveRequestsLocally(reqs: VerificationRequestDto[]): void {
    try {
      localStorage.setItem(this.REQUESTS_KEY, JSON.stringify(reqs));
    } catch (e) {
      console.error('Failed to save requests locally', e);
    }
    this.verificationRequests.set(reqs);
  }

  private getStoredRequests(): VerificationRequestDto[] {
    try {
      const raw = localStorage.getItem(this.REQUESTS_KEY);
      if (raw) {
        const parsed = JSON.parse(raw);
        if (Array.isArray(parsed)) {
          return parsed;
        }
      }
      return [];
    } catch {
      return [];
    }
  }

  private getStoredProfile(): BusinessProfileDto | null {
    try {
      const raw = localStorage.getItem(this.PROFILE_KEY);
      if (raw) {
        const parsed = JSON.parse(raw);
        if (parsed && (parsed.id === 'd1111111-2222-3333-4444-555555555555' || parsed.legalName === 'ABC MANUFACTURING PRIVATE LIMITED')) {
          localStorage.removeItem(this.PROFILE_KEY);
          return null;
        }
        return parsed;
      }
      return null;
    } catch {
      return null;
    }
  }

  saveProfileLocally(profile: BusinessProfileDto): void {
    try {
      localStorage.setItem(this.PROFILE_KEY, JSON.stringify(profile));
    } catch (e) {
      console.error('Failed to save profile locally', e);
    }
    this.currentProfile.set(profile);
  }

  clearStoredProfile(): void {
    localStorage.removeItem(this.PROFILE_KEY);
    this.currentProfile.set(null);
  }

  private getEmptyProfile(id: string): BusinessProfileDto {
    return {
      id: id,
      organizationId: id,
      businessIdNumber: '',
      legalName: '',
      tradeName: '',
      businessType: 'Private Limited Company',
      incorporationDate: '',
      pan: '',
      gstin: '',
      cin: '',
      udyamNumber: '',
      website: '',
      employeeCount: 0,
      description: '',
      verificationStatus: 'UNVERIFIED',
      trustScore: 0,
      addresses: [],
      directors: [],
      bankAccounts: []
    };
  }

  private getEmptyDashboard(id: string, profile?: BusinessProfileDto | null): BusinessDashboardDto {
    return {
      businessId: id,
      businessName: profile?.legalName || profile?.tradeName || 'Registered Enterprise',
      businessIdNumber: profile?.businessIdNumber || 'VP-IN-PENDING',
      overallTrustScore: profile?.trustScore || 0,
      identityScore: 0,
      credentialScore: 0,
      complianceScore: 0,
      verificationScore: 0,
      riskScore: 0,
      verificationStatus: profile?.verificationStatus || 'UNVERIFIED',
      totalCredentials: 0,
      verifiedCredentials: 0,
      pendingVerificationCount: 0,
      activeAlertsCount: 0,
      connectedBuyersCount: 0,
      recentActivities: []
    };
  }
}
