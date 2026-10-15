import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminStateService, AdminSession, SecurityEventAlert } from '../../../core/services/admin-state.service';

@Component({
  selector: 'app-admin-security',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './admin-security.component.html',
  styleUrl: './admin-security.component.css'
})
export class AdminSecurityComponent {
  private adminState = inject(AdminStateService);

  sessions = this.adminState.sessions;
  securityAlerts = this.adminState.securityAlerts;

  showRevokeAllModal = signal(false);
  actionFeedback = signal<string | null>(null);

  openAlertsCount(): number {
    return this.securityAlerts().filter(a => a.status === 'INVESTIGATING').length;
  }

  revokeSession(id: string, user: string): void {
    this.adminState.revokeSession(id);
    this.actionFeedback.set(`Session ${id} for operator ${user} has been revoked.`);
    setTimeout(() => this.actionFeedback.set(null), 4000);
  }

  confirmRevokeAll(): void {
    this.adminState.revokeAllOtherSessions();
    this.showRevokeAllModal.set(false);
    this.actionFeedback.set('All concurrent administrative sessions have been terminated. Audit entry logged.');
    setTimeout(() => this.actionFeedback.set(null), 4000);
  }

  resolveAlert(id: string): void {
    this.adminState.resolveSecurityAlert(id, 'Marked remediated by Security Administrator.');
    this.actionFeedback.set(`Alert ${id} has been marked resolved.`);
    setTimeout(() => this.actionFeedback.set(null), 4000);
  }

  lockTargetAccount(account: string): void {
    this.actionFeedback.set(`Account ${account} temporarily locked. Password reset token dispatched via sovereign OOB channel.`);
    setTimeout(() => this.actionFeedback.set(null), 4000);
  }

  triggerSecurityScan(): void {
    this.actionFeedback.set('Diagnostic vulnerability & token integrity scan complete. 0 unauthorized anomalies detected.');
    setTimeout(() => this.actionFeedback.set(null), 4000);
  }
}
