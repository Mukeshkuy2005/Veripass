import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { VerifierProfileService } from '../../../core/services/verifier-profile.service';
import { AuthService } from '../../../core/auth/auth.service';
import {
  VerifierProfileResponse,
  VerifierSession,
  VerifierLoginHistoryItem,
  VerifierActivity
} from '../../../core/models/verifier-profile.models';
import {
  formatIstDateTime,
  formatRelativeTime
} from '../../../core/utils/ist-date-formatter';

@Component({
  selector: 'vp-verifier-profile',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  templateUrl: './verifier-profile.component.html',
  styleUrl: './verifier-profile.component.css'
})
export class VerifierProfileComponent implements OnInit {
  private profileService = inject(VerifierProfileService);
  private authService = inject(AuthService);

  // Core State Signals
  profileData = signal<VerifierProfileResponse | null>(null);
  loading = signal<boolean>(true);
  error = signal<string | null>(null);
  refreshing = signal<boolean>(false);
  lastUpdatedTimestamp = signal<Date | null>(null);

  // Activity State Signals
  selectedRange = signal<string>('30d');
  activityLoading = signal<boolean>(false);
  activityError = signal<string | null>(null);

  // Modal / Drawer Signals
  showSessionsModal = signal<boolean>(false);
  sessionsList = signal<VerifierSession[]>([]);
  sessionsLoading = signal<boolean>(false);

  showRevokeConfirmModal = signal<boolean>(false);
  revokingOthers = signal<boolean>(false);

  showLoginHistoryDrawer = signal<boolean>(false);
  loginHistoryList = signal<VerifierLoginHistoryItem[]>([]);
  loginHistoryLoading = signal<boolean>(false);

  showMfaModal = signal<boolean>(false);
  mfaToggling = signal<boolean>(false);

  // Clipboard & Toast Feedback
  copiedField = signal<string | null>(null);
  toast = signal<{ message: string; type: 'success' | 'info' | 'error' } | null>(null);

  // Computed Properties
  userInitials = computed(() => {
    const p = this.profileData()?.profile;
    if (!p?.fullName) return 'VO';
    const parts = p.fullName.trim().split(' ');
    if (parts.length >= 2) {
      return (parts[0][0] + parts[1][0]).toUpperCase();
    }
    return p.fullName.substring(0, 2).toUpperCase();
  });

  lastUpdatedLabel = computed(() => {
    const ts = this.lastUpdatedTimestamp();
    if (!ts) return '';
    const formatted = formatIstDateTime(ts);
    return `Last updated ${formatted.timePart}`;
  });

  lastLoginFormatted = computed(() => {
    const raw = this.profileData()?.security.lastLoginAtUtc;
    if (!raw) return '—';
    const formatted = formatIstDateTime(raw);
    const rel = formatRelativeTime(raw);
    return `${formatted.datePart} · ${formatted.timePart} (${rel})`;
  });

  ngOnInit(): void {
    this.loadProfile();
  }

  // Load Primary Profile Data
  loadProfile(): void {
    this.loading.set(true);
    this.error.set(null);

    this.profileService.getProfile(this.selectedRange()).subscribe({
      next: (res) => {
        this.profileData.set(res);
        this.lastUpdatedTimestamp.set(new Date());
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err?.error?.message || 'Could not load your verifier profile. Please try again.');
        this.loading.set(false);
      }
    });
  }

  // Refresh Profile & Re-sync State
  refresh(): void {
    if (this.refreshing()) return;
    this.refreshing.set(true);

    this.profileService.getProfile(this.selectedRange()).subscribe({
      next: (res) => {
        this.profileData.set(res);
        this.lastUpdatedTimestamp.set(new Date());
        this.refreshing.set(false);
        this.showToast('Profile refreshed successfully.', 'success');
      },
      error: (err) => {
        this.refreshing.set(false);
        this.showToast('Unable to refresh profile at this time.', 'error');
      }
    });
  }

  // Change Operational Activity Date Range
  onRangeChange(newRange: string): void {
    if (this.selectedRange() === newRange) return;
    this.selectedRange.set(newRange);
    this.activityLoading.set(true);
    this.activityError.set(null);

    this.profileService.getActivity(newRange).subscribe({
      next: (activity) => {
        const cur = this.profileData();
        if (cur) {
          this.profileData.set({
            ...cur,
            activity
          });
        }
        this.activityLoading.set(false);
      },
      error: () => {
        this.activityError.set('Could not load activity for the selected range.');
        this.activityLoading.set(false);
      }
    });
  }

  // Copy Helpers
  copyText(text: string, fieldKey: string, label: string): void {
    navigator.clipboard.writeText(text).then(() => {
      this.copiedField.set(fieldKey);
      this.showToast(`${label} copied to clipboard`, 'info');
      setTimeout(() => {
        if (this.copiedField() === fieldKey) {
          this.copiedField.set(null);
        }
      }, 2000);
    }).catch(() => {
      this.showToast(`Unable to copy ${label}`, 'error');
    });
  }

  // --- Session Management ---

  openSessionsModal(): void {
    this.showSessionsModal.set(true);
    this.loadSessions();
  }

  closeSessionsModal(): void {
    this.showSessionsModal.set(false);
  }

  loadSessions(): void {
    this.sessionsLoading.set(true);
    this.profileService.getSessions().subscribe({
      next: (sessions) => {
        this.sessionsList.set(sessions);
        this.sessionsLoading.set(false);
      },
      error: () => {
        this.sessionsLoading.set(false);
        this.showToast('Unable to load active sessions list.', 'error');
      }
    });
  }

  promptSignOutOthers(): void {
    this.showRevokeConfirmModal.set(true);
  }

  cancelSignOutOthers(): void {
    this.showRevokeConfirmModal.set(false);
  }

  confirmSignOutOthers(): void {
    this.revokingOthers.set(true);
    this.profileService.revokeOtherSessions().subscribe({
      next: (res) => {
        this.revokingOthers.set(false);
        this.showRevokeConfirmModal.set(false);
        this.showToast(res.message || 'Signed out of all other sessions.', 'success');

        // Update local session count
        const cur = this.profileData();
        if (cur) {
          this.profileData.set({
            ...cur,
            security: {
              ...cur.security,
              activeSessionCount: 1
            }
          });
        }

        // Refresh sessions list if modal is open
        if (this.showSessionsModal()) {
          this.loadSessions();
        }
      },
      error: () => {
        this.revokingOthers.set(false);
        this.showToast('Could not sign out the other sessions. Try again.', 'error');
      }
    });
  }

  revokeSingleSession(session: VerifierSession): void {
    this.profileService.revokeSession(session.id).subscribe({
      next: (res) => {
        this.showToast(res.message || 'Session revoked successfully.', 'success');
        this.sessionsList.update(list => list.filter(s => s.id !== session.id));

        const cur = this.profileData();
        if (cur) {
          const newCount = Math.max(1, cur.security.activeSessionCount - 1);
          this.profileData.set({
            ...cur,
            security: {
              ...cur.security,
              activeSessionCount: newCount
            }
          });
        }
      },
      error: () => {
        this.showToast('Unable to revoke session. Please try again.', 'error');
      }
    });
  }

  // --- Login History ---

  openLoginHistoryDrawer(): void {
    this.showLoginHistoryDrawer.set(true);
    this.loadLoginHistory();
  }

  closeLoginHistoryDrawer(): void {
    this.showLoginHistoryDrawer.set(false);
  }

  loadLoginHistory(): void {
    this.loginHistoryLoading.set(true);
    this.profileService.getLoginHistory().subscribe({
      next: (history) => {
        this.loginHistoryList.set(history);
        this.loginHistoryLoading.set(false);
      },
      error: () => {
        this.loginHistoryLoading.set(false);
        this.showToast('Unable to load login history records.', 'error');
      }
    });
  }

  // --- MFA Management ---

  openMfaModal(): void {
    this.showMfaModal.set(true);
  }

  closeMfaModal(): void {
    this.showMfaModal.set(false);
  }

  toggleMfa(enable: boolean): void {
    this.mfaToggling.set(true);
    this.profileService.toggleMfa(enable).subscribe({
      next: (res) => {
        this.mfaToggling.set(false);
        this.showMfaModal.set(false);

        const cur = this.profileData();
        if (cur) {
          this.profileData.set({
            ...cur,
            security: {
              ...cur.security,
              mfaEnabled: res.mfaEnabled
            }
          });
        }
        this.showToast(res.message || `MFA ${enable ? 'enabled' : 'disabled'} successfully.`, 'success');
      },
      error: () => {
        this.mfaToggling.set(false);
        this.showToast('Failed to update MFA configuration.', 'error');
      }
    });
  }

  // Format Helper: Seconds to "Xm Ys"
  formatReviewTime(seconds: number): string {
    if (!seconds || seconds <= 0) return '—';
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    if (m === 0) return `${s}s`;
    if (s === 0) return `${m}m`;
    return `${m}m ${s}s`;
  }

  // Format Helper: Timestamp to IST representation
  formatIst(dateStr?: string | Date | null): string {
    if (!dateStr) return '—';
    const formatted = formatIstDateTime(dateStr);
    return formatted.fullFormatted;
  }

  // Format Helper: Relative time
  formatRel(dateStr?: string | Date | null): string {
    if (!dateStr) return '';
    return formatRelativeTime(dateStr);
  }

  // Toast System
  private showToast(message: string, type: 'success' | 'info' | 'error' = 'info'): void {
    this.toast.set({ message, type });
    setTimeout(() => {
      this.toast.set(null);
    }, 4000);
  }
}
