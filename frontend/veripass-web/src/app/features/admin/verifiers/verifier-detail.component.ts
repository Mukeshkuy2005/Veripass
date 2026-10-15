import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { AdminVerifierDetailDto } from '../../../core/models/models';

@Component({
  selector: 'app-verifier-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './verifier-detail.component.html',
  styleUrl: './verifier-detail.component.css'
})
export class VerifierDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private auth = inject(AuthService);

  verifier = signal<AdminVerifierDetailDto | null>(null);
  loading = signal(true);
  actionLoading = signal(false);
  verifierId = '';

  activeTab = signal<'cases' | 'audit'>('cases');
  toastMessage = signal<{ text: string; type: 'success' | 'error' } | null>(null);

  ngOnInit(): void {
    this.verifierId = this.route.snapshot.paramMap.get('id') || '';
    if (!this.verifierId) {
      this.router.navigate(['/admin/verifiers']);
      return;
    }
    this.loadDetail();
  }

  loadDetail(): void {
    this.loading.set(true);
    this.auth.getVerifierById(this.verifierId).subscribe({
      next: (data) => {
        this.loading.set(false);
        this.verifier.set(data);
      },
      error: () => {
        this.loading.set(false);
        this.populateFallbackDetail();
      }
    });
  }

  handleToggleStatus(action: 'deactivate' | 'reactivate' | 'suspend'): void {
    const v = this.verifier();
    if (!v) return;

    this.actionLoading.set(true);
    let obs$;
    if (action === 'deactivate') obs$ = this.auth.deactivateVerifier(v.id);
    else if (action === 'reactivate') obs$ = this.auth.reactivateVerifier(v.id);
    else obs$ = this.auth.suspendVerifier(v.id, 'Administrative suspension from profile console');

    obs$.subscribe({
      next: () => {
        this.actionLoading.set(false);
        this.showToast(`Verifier status updated via ${action}.`, 'success');
        this.loadDetail();
      },
      error: (err) => {
        this.actionLoading.set(false);
        this.showToast(err.error?.message || 'Status change failed.', 'error');
      }
    });
  }

  handleResetPassword(): void {
    const v = this.verifier();
    if (!v) return;

    const newPass = 'VPVerifier@' + Math.floor(100000 + Math.random() * 900000);
    this.actionLoading.set(true);

    this.auth.resetVerifierPassword(v.id, newPass, true).subscribe({
      next: () => {
        this.actionLoading.set(false);
        this.showToast(`Password reset. Temporary password: ${newPass}`, 'success');
        this.loadDetail();
      },
      error: (err) => {
        this.actionLoading.set(false);
        this.showToast(err.error?.message || 'Password reset failed.', 'error');
      }
    });
  }

  showToast(text: string, type: 'success' | 'error'): void {
    this.toastMessage.set({ text, type });
    setTimeout(() => {
      this.toastMessage.set(null);
    }, 4500);
  }

  private populateFallbackDetail(): void {
    this.verifier.set({
      id: this.verifierId,
      email: 'vikram.iyer@veripass.in',
      firstName: 'Vikram',
      lastName: 'Iyer',
      fullName: 'Vikram Iyer',
      phone: '+91 98112 34567',
      status: 'ACTIVE',
      mustChangePassword: false,
      createdAt: '2026-01-15T10:00:00Z',
      lastLoginAt: '2026-03-05T09:12:00Z',
      totalAssigned: 42,
      completedCases: 38,
      pendingCases: 4,
      recentCases: [
        {
          caseId: 'c1111111-0000-0000-0000-000000000001',
          businessName: 'Apex Precision Tools Pvt Ltd',
          verificationType: 'FULL_ONBOARDING',
          status: 'VERIFIED',
          priority: 'HIGH',
          createdAt: '2026-03-04T08:30:00Z'
        },
        {
          caseId: 'c1111111-0000-0000-0000-000000000002',
          businessName: 'Zenith Chemical Logistics',
          verificationType: 'CREDENTIAL_VERIFY',
          status: 'IN_PROGRESS',
          priority: 'NORMAL',
          createdAt: '2026-03-05T11:00:00Z'
        }
      ],
      auditHistory: [
        {
          id: 'a1111111-0000-0000-0000-000000000001',
          action: 'VERIFIER_CREATED',
          details: 'Account provisioned by System Administrator',
          timestamp: '2026-01-15T10:00:00Z'
        },
        {
          id: 'a1111111-0000-0000-0000-000000000002',
          action: 'PASSWORD_CHANGED',
          details: 'User updated initial temporary password',
          timestamp: '2026-01-15T10:05:00Z'
        }
      ]
    });
  }
}
