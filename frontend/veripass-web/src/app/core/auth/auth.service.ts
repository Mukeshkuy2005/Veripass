import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap, catchError, of } from 'rxjs';
import { ApiService } from '../services/api.service';
import {
  AuthResponseDto,
  UserDto,
  AdminVerifierSummaryDto,
  AdminVerifierDetailDto,
  AdminVerifiersPagedResponse,
  CreateVerifierRequest,
  UpdateVerifierRequest
} from '../models/models';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private api = inject(ApiService);
  private router = inject(Router);

  private readonly TOKEN_KEY = 'veripass_token';
  private readonly USER_KEY = 'veripass_user';

  // Angular 19 Signals for reactive state
  currentUser = signal<UserDto | null>(this.getStoredUser());
  token = signal<string | null>(localStorage.getItem(this.TOKEN_KEY));

  isAuthenticated = computed(() => !!this.token() && !!this.currentUser());
  currentRole = computed(() => this.currentUser()?.role || 'SUPPLIER');
  currentBusinessId = signal<string>(this.currentUser()?.currentOrganizationId || '');

  constructor() {
    // Only stay with the registered user. Do not overwrite user session with hardcoded demo data.
  }

  register(data: any): Observable<AuthResponseDto> {
    return this.api.post<AuthResponseDto>('auth/register', data).pipe(
      tap(res => this.handleAuthSuccess(res))
    );
  }

  login(credentials: { email: string; password: string }): Observable<AuthResponseDto> {
    return this.api.post<AuthResponseDto>('auth/login', credentials).pipe(
      tap(res => {
        if (!res.requiresMfa) {
          this.handleAuthSuccess(res);
        }
      })
    );
  }

  verifyMfaLogin(data: { email: string; code: string; ticket?: string }): Observable<AuthResponseDto> {
    return this.api.post<AuthResponseDto>('auth/mfa/verify-login', data).pipe(
      tap(res => this.handleAuthSuccess(res))
    );
  }

  resendMfaOtp(email: string): Observable<{ success: boolean; message: string }> {
    return this.api.post<{ success: boolean; message: string }>('auth/mfa/send-otp', { email });
  }

  sendMfaSettingsOtp(email?: string): Observable<{ success: boolean; message: string }> {
    return this.api.post<{ success: boolean; message: string }>('auth/mfa/send-otp', { email });
  }

  logout(): void {
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.USER_KEY);
    localStorage.removeItem('veripass_business_profile');
    this.token.set(null);
    this.currentUser.set(null);
    this.router.navigate(['/auth/login']);
  }

  switchDemoRole(role: 'SUPPLIER' | 'BUYER' | 'VERIFIER' | 'ADMIN'): void {
    const demoUser: UserDto = {
      id: role === 'BUYER' ? 'a1111111-0000-0000-0000-000000000001' : '99999999-0000-0000-0000-000000000001',
      email: `${role.toLowerCase()}@veripass.in`,
      firstName: role === 'SUPPLIER' ? 'Rajesh' : role === 'BUYER' ? 'Enterprise' : role === 'VERIFIER' ? 'Vikram' : 'Admin',
      lastName: role === 'SUPPLIER' ? 'Sharma' : role === 'BUYER' ? 'Buyer' : role === 'VERIFIER' ? 'Iyer' : 'System',
      role: role,
      companyName: role === 'SUPPLIER' ? 'ABC INDUSTRIAL DYNAMICS' : 'Enterprise Buyer Corp',
      organizationName: role === 'SUPPLIER' ? 'ABC INDUSTRIAL DYNAMICS' : 'Enterprise Buyer Corp',
      businessType: role === 'SUPPLIER' ? 'Private Limited Company' : 'Public Limited Company',
      currentOrganizationId: role === 'BUYER' ? 'b1111111-0000-0000-0000-000000000001' : '88888888-0000-0000-0000-000000000001',
      permissions: ['*']
    };

    const dummyToken = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.demo_token';
    localStorage.setItem(this.TOKEN_KEY, dummyToken);
    localStorage.setItem(this.USER_KEY, JSON.stringify(demoUser));
    this.token.set(dummyToken);
    this.currentUser.set(demoUser);
    this.currentBusinessId.set(demoUser.currentOrganizationId || '');
  }

  setMockSession(role: string): void {
    const r = role.toUpperCase() as 'SUPPLIER' | 'BUYER' | 'VERIFIER' | 'ADMIN';
    this.switchDemoRole(r || 'BUYER');
  }

  registerUserWithRole(
    role: 'SUPPLIER' | 'BUYER' | 'VERIFIER' | 'ADMIN',
    details: {
      email: string;
      fullName: string;
      companyName?: string;
      entityType?: string;
      mobileNumber?: string;
    }
  ): void {
    const parts = (details.fullName || 'User').trim().split(' ');
    const firstName = parts[0] || 'User';
    const lastName = parts.slice(1).join(' ') || (role === 'SUPPLIER' ? 'Business' : role);

    const newUser: UserDto = {
      id: 'usr-' + Date.now(),
      email: details.email || `${role.toLowerCase()}@veripass.in`,
      firstName: firstName,
      lastName: lastName,
      phone: details.mobileNumber,
      companyName: details.companyName,
      organizationName: details.companyName,
      businessType: details.entityType,
      role: role,
      currentOrganizationId: 'org-' + Date.now(),
      permissions: ['*']
    };

    const dummyToken = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.demo_token';
    localStorage.setItem(this.TOKEN_KEY, dummyToken);
    localStorage.setItem(this.USER_KEY, JSON.stringify(newUser));
    this.token.set(dummyToken);
    this.currentUser.set(newUser);
  }

  changePassword(currentPassword: string | null, newPassword: string): Observable<{ success: boolean; message: string }> {
    return this.api.post<{ success: boolean; message: string }>('auth/change-password', { currentPassword, newPassword }).pipe(
      tap(() => {
        const u = this.currentUser();
        if (u) {
          const updated = { ...u, mustChangePassword: false };
          this.currentUser.set(updated);
          localStorage.setItem(this.USER_KEY, JSON.stringify(updated));
        }
      })
    );
  }

  toggleMfa(enable: boolean, method: string = 'EMAIL', verificationCode?: string): Observable<{ mfaEnabled: boolean; method: string; message: string }> {
    return this.api.post<{ mfaEnabled: boolean; method: string; message: string }>('auth/mfa/toggle', {
      enable,
      method,
      verificationCode
    }).pipe(
      tap(res => {
        const u = this.currentUser();
        if (u) {
          const updated = { ...u, mfaEnabled: res.mfaEnabled };
          this.currentUser.set(updated as any);
          localStorage.setItem(this.USER_KEY, JSON.stringify(updated));
        }
      })
    );
  }

  revokeSession(sessionId: string): Observable<{ success: boolean; message: string }> {
    return this.api.post<{ success: boolean; message: string }>('auth/sessions/revoke', { sessionId });
  }

  updateProfile(data: { fullName: string; email: string; phone?: string }): Observable<UserDto> {
    const current = this.currentUser();
    const parts = (data.fullName || '').trim().split(' ');
    const firstName = parts[0] || (current ? current.firstName : 'User');
    const lastName = parts.slice(1).join(' ') || (current ? current.lastName : '');

    // Optimistically update the signal and localStorage immediately so the whole window (sidebar, topbar, tabs) updates instantaneously
    const updatedUser: UserDto = {
      ...(current || {
        id: '99999999-0000-0000-0000-000000000001',
        role: 'SUPPLIER',
        permissions: ['*']
      }),
      firstName,
      lastName,
      fullName: data.fullName.trim(),
      email: data.email.trim(),
      phone: data.phone?.trim()
    };

    this.currentUser.set(updatedUser);
    localStorage.setItem(this.USER_KEY, JSON.stringify(updatedUser));

    // Call backend API to persist to PostgreSQL database
    return this.api.put<UserDto>('auth/profile', {
      fullName: data.fullName.trim(),
      email: data.email.trim(),
      phone: data.phone?.trim()
    }).pipe(
      tap((saved) => {
        if (saved) {
          const finalUser: UserDto = {
            ...updatedUser,
            ...saved,
            fullName: `${saved.firstName || ''} ${saved.lastName || ''}`.trim() || data.fullName.trim()
          };
          this.currentUser.set(finalUser);
          localStorage.setItem(this.USER_KEY, JSON.stringify(finalUser));
        }
      }),
      catchError(() => of(updatedUser))
    );
  }

  // Admin Verifier Management Endpoints
  getVerifiers(search?: string, status?: string, page = 1, pageSize = 20): Observable<AdminVerifiersPagedResponse> {
    const params: any = { page, pageSize };
    if (search) params.search = search;
    if (status && status !== 'ALL') params.status = status;
    return this.api.get<AdminVerifiersPagedResponse>('admin/verifiers', params);
  }

  getVerifierById(id: string): Observable<AdminVerifierDetailDto> {
    return this.api.get<AdminVerifierDetailDto>(`admin/verifiers/${id}`);
  }

  createVerifier(data: CreateVerifierRequest): Observable<AdminVerifierSummaryDto> {
    return this.api.post<AdminVerifierSummaryDto>('admin/verifiers', data);
  }

  updateVerifier(id: string, data: UpdateVerifierRequest): Observable<AdminVerifierSummaryDto> {
    return this.api.put<AdminVerifierSummaryDto>(`admin/verifiers/${id}`, data);
  }

  deactivateVerifier(id: string, reason?: string): Observable<any> {
    return this.api.post(`admin/verifiers/${id}/deactivate`, { reason });
  }

  reactivateVerifier(id: string): Observable<any> {
    return this.api.post(`admin/verifiers/${id}/reactivate`, {});
  }

  suspendVerifier(id: string, reason: string): Observable<any> {
    return this.api.post(`admin/verifiers/${id}/suspend`, { reason });
  }

  resetVerifierPassword(id: string, newPassword: string, mustChangePassword = true): Observable<any> {
    return this.api.post(`admin/verifiers/${id}/reset-password`, { newPassword, mustChangePassword });
  }

  deleteVerifier(id: string): Observable<any> {
    return this.api.delete(`admin/verifiers/${id}`);
  }

  private handleAuthSuccess(res: AuthResponseDto): void {
    localStorage.setItem(this.TOKEN_KEY, res.accessToken);
    const user: UserDto = {
      id: res.userId,
      email: res.email,
      firstName: res.fullName.split(' ')[0] || '',
      lastName: res.fullName.split(' ').slice(1).join(' ') || '',
      fullName: res.fullName,
      companyName: res.organizationName,
      organizationName: res.organizationName,
      currentOrganizationId: res.organizationId,
      role: res.role,
      permissions: res.permissions,
      mustChangePassword: res.mustChangePassword
    };
    localStorage.setItem(this.USER_KEY, JSON.stringify(user));
    this.token.set(res.accessToken);
    this.currentUser.set(user);
    if (res.organizationId) {
      this.currentBusinessId.set(res.organizationId);
    }
  }

  private getStoredUser(): UserDto | null {
    const data = localStorage.getItem(this.USER_KEY);
    if (!data) return null;
    try {
      const user = JSON.parse(data);
      if (user && (user.email === 'buyer@veripass.in' || user.email === 'supplier@veripass.in' || user.id?.startsWith('a1111111') || user.id?.startsWith('99999999') || user.companyName === 'Tata Industries Pvt Ltd')) {
        localStorage.removeItem(this.USER_KEY);
        localStorage.removeItem(this.TOKEN_KEY);
        return null;
      }
      return user;
    } catch {
      return null;
    }
  }
}
