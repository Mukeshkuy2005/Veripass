import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import {
  VerifierProfileResponse,
  VerifierActivity,
  VerifierSession,
  VerifierLoginHistoryItem
} from '../models/verifier-profile.models';

@Injectable({
  providedIn: 'root'
})
export class VerifierProfileService {
  private api = inject(ApiService);

  /**
   * Retrieves full verifier account profile, jurisdictional scope, security posture, and activity summary.
   */
  getProfile(range: string = '30d'): Observable<VerifierProfileResponse> {
    return this.api.get<VerifierProfileResponse>('verifier/profile', { range });
  }

  /**
   * Retrieves operational activity summary for a specified period (e.g., '7d', '30d', '90d', 'all').
   */
  getActivity(range: string = '30d'): Observable<VerifierActivity> {
    return this.api.get<VerifierActivity>('verifier/profile/activity', { range });
  }

  /**
   * Retrieves active authenticated sessions for the current verifier.
   */
  getSessions(): Observable<VerifierSession[]> {
    return this.api.get<VerifierSession[]>('verifier/profile/sessions');
  }

  /**
   * Signs out all other active sessions except the current session.
   */
  revokeOtherSessions(): Observable<{ success: boolean; revokedCount: number; message: string }> {
    return this.api.post<{ success: boolean; revokedCount: number; message: string }>('verifier/profile/sessions/revoke-others', {});
  }

  /**
   * Revokes a specific authenticated session.
   */
  revokeSession(sessionId: string): Observable<{ success: boolean; message: string }> {
    return this.api.post<{ success: boolean; message: string }>(`verifier/profile/sessions/${sessionId}/revoke`, {});
  }

  /**
   * Retrieves authentic login and security audit history for the current verifier.
   */
  getLoginHistory(): Observable<VerifierLoginHistoryItem[]> {
    return this.api.get<VerifierLoginHistoryItem[]>('verifier/profile/login-history');
  }

  /**
   * Toggles Multi-Factor Authentication (MFA) status for the verifier account.
   */
  toggleMfa(enable: boolean): Observable<{ success: boolean; mfaEnabled: boolean; message: string }> {
    return this.api.post<{ success: boolean; mfaEnabled: boolean; message: string }>('verifier/profile/mfa/toggle', { enable });
  }
}
