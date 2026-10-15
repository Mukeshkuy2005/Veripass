import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule, Router } from '@angular/router';
import { OnboardingService } from '../../../core/services/onboarding.service';
import {
  OnboardingRequestDetail,
  OnboardingRequestRequirement
} from '../../../core/models/models';

@Component({
  selector: 'app-buyer-onboarding-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './buyer-onboarding-detail.component.html',
  styleUrl: './buyer-onboarding-detail.component.css'
})
export class BuyerOnboardingDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private onboardingService = inject(OnboardingService);

  requestId = signal<string>('');
  detail = signal<OnboardingRequestDetail | null>(null);
  loading = signal<boolean>(true);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);

  // Expanded requirement card ID
  expandedRequirementId = signal<string | null>(null);

  // Approval Modal State
  isApproveModalOpen = signal<boolean>(false);
  approveErpCode = signal<string>('');
  approvePaymentTerms = signal<string>('Net 45 Days via RTGS');
  approveTargetPlant = signal<string>('');
  isApproving = signal<boolean>(false);

  // Rejection Modal State
  isRejectModalOpen = signal<boolean>(false);
  rejectionReason = signal<string>('');
  isRejecting = signal<boolean>(false);

  // Message / Remind State
  isReminding = signal<boolean>(false);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.requestId.set(id);
      this.loadDetail(id);
    } else {
      this.errorMsg.set('Invalid onboarding request ID.');
      this.loading.set(false);
    }
  }

  loadDetail(id: string): void {
    this.loading.set(true);
    this.errorMsg.set(null);

    this.onboardingService.getOnboardingRequest(id).subscribe({
      next: (data) => {
        this.detail.set(data);
        if (data.erpVendorCode) {
          this.approveErpCode.set(data.erpVendorCode);
        }
        if (data.paymentTerms) {
          this.approvePaymentTerms.set(data.paymentTerms);
        }
        if (data.targetPlant) {
          this.approveTargetPlant.set(data.targetPlant);
        }
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load onboarding detail', err);
        this.errorMsg.set('Unable to retrieve onboarding workspace from server.');
        this.loading.set(false);
      }
    });
  }

  toggleRequirementExpand(reqId: string): void {
    if (this.expandedRequirementId() === reqId) {
      this.expandedRequirementId.set(null);
    } else {
      this.expandedRequirementId.set(reqId);
    }
  }

  // Stepper calculations
  getStepperStep(): number {
    const d = this.detail();
    if (!d) return 1;
    switch (d.status) {
      case 'NEW': return 1;
      case 'REQUESTED': return 2;
      case 'ACTION_REQUIRED': return 3;
      case 'IN_VERIFICATION': return 4;
      case 'APPROVED': return 6;
      case 'REJECTED': return 5;
      case 'WITHDRAWN': return 1;
      default: return 3;
    }
  }

  // Actions
  openApproveModal(): void {
    this.isApproveModalOpen.set(true);
    if (!this.approveErpCode()) {
      this.approveErpCode.set(`SAP-VND-${Math.floor(10000 + Math.random() * 90000)}`);
    }
  }

  closeApproveModal(): void {
    this.isApproveModalOpen.set(false);
  }

  confirmApprove(): void {
    const d = this.detail();
    if (!d) return;

    this.isApproving.set(true);
    this.onboardingService.approve(d.id, {
      erpVendorCode: this.approveErpCode().trim(),
      paymentTerms: this.approvePaymentTerms().trim(),
      targetPlant: this.approveTargetPlant().trim()
    }).subscribe({
      next: (res) => {
        this.isApproving.set(false);
        this.closeApproveModal();
        this.successMsg.set(res.message || 'Supplier successfully approved and empanelled!');
        this.loadDetail(d.id);
      },
      error: (err) => {
        console.error('Approval failed', err);
        this.isApproving.set(false);
        this.errorMsg.set(err.error?.message || 'Approval could not be completed.');
      }
    });
  }

  openRejectModal(): void {
    this.isRejectModalOpen.set(true);
    this.rejectionReason.set('');
  }

  closeRejectModal(): void {
    this.isRejectModalOpen.set(false);
  }

  confirmReject(): void {
    const d = this.detail();
    if (!d || !this.rejectionReason().trim()) return;

    this.isRejecting.set(true);
    this.onboardingService.reject(d.id, { reason: this.rejectionReason().trim() }).subscribe({
      next: (res) => {
        this.isRejecting.set(false);
        this.closeRejectModal();
        this.successMsg.set(res.message || 'Onboarding request rejected.');
        this.loadDetail(d.id);
      },
      error: (err) => {
        console.error('Rejection failed', err);
        this.isRejecting.set(false);
        this.errorMsg.set(err.error?.message || 'Rejection could not be completed.');
      }
    });
  }

  sendReminder(): void {
    const d = this.detail();
    if (!d) return;
    this.isReminding.set(true);
    this.onboardingService.remind(d.id).subscribe({
      next: () => {
        this.isReminding.set(false);
        this.successMsg.set('Reminder notification sent to supplier.');
        setTimeout(() => this.successMsg.set(null), 3000);
      },
      error: (err) => {
        console.error('Reminder failed', err);
        this.isReminding.set(false);
      }
    });
  }

  withdrawRequest(): void {
    const d = this.detail();
    if (!d || !confirm('Are you sure you want to withdraw this active onboarding request?')) return;

    this.onboardingService.withdraw(d.id).subscribe({
      next: (res) => {
        this.successMsg.set(res.message || 'Onboarding request withdrawn.');
        this.loadDetail(d.id);
      },
      error: (err) => {
        console.error('Withdrawal failed', err);
        this.errorMsg.set(err.error?.message || 'Unable to withdraw request.');
      }
    });
  }

  navigateBack(): void {
    this.router.navigate(['/buyer/onboarding']);
  }

  viewPassport(): void {
    const d = this.detail();
    if (d?.supplierBusinessId) {
      this.router.navigate(['/buyer/suppliers', d.supplierBusinessId, 'passport']);
    }
  }

  formatDate(dateStr?: string): string {
    if (!dateStr) return '—';
    const d = new Date(dateStr);
    return d.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' });
  }

  formatTimestamp(dateStr?: string): string {
    if (!dateStr) return '—';
    const d = new Date(dateStr);
    return `${d.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' })} at ${d.toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit' })}`;
  }
}
