import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AdminStateService, AdminDispute } from '../../../core/services/admin-state.service';

@Component({
  selector: 'app-dispute-management',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './dispute-management.component.html',
  styleUrl: './dispute-management.component.css'
})
export class DisputeManagementComponent {
  private adminState = inject(AdminStateService);

  disputes = this.adminState.disputes;
  selectedDispute = signal<AdminDispute | null>(null);
  activeModalDispute = signal<AdminDispute | null>(null);

  resolutionAction: 'UPHOLD' | 'REVERIFY' | 'ESCALATE' = 'REVERIFY';
  resolutionReason = 'Corroborating secondary utility evidence confirms spelling variance is due to regional municipality reclassification. Returning to senior officer for reverification.';

  selectDispute(d: AdminDispute): void {
    this.selectedDispute.set(d);
  }

  openResolutionModal(d: AdminDispute): void {
    this.activeModalDispute.set(d);
  }

  closeModal(): void {
    this.activeModalDispute.set(null);
  }

  submitResolution(disputeId: string): void {
    if (!this.resolutionReason.trim()) return;
    this.adminState.resolveDispute(disputeId, this.resolutionAction, this.resolutionReason);
    this.closeModal();
  }
}
