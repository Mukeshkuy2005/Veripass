import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import {
  AdminVerifierSummaryDto,
  AdminVerifierStatsDto,
  CreateVerifierRequest
} from '../../../core/models/models';

@Component({
  selector: 'app-verifier-management',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './verifier-management.component.html',
  styleUrl: './verifier-management.component.css'
})
export class VerifierManagementComponent implements OnInit {
  private auth = inject(AuthService);

  verifiers = signal<AdminVerifierSummaryDto[]>([]);
  stats = signal<AdminVerifierStatsDto>({
    totalVerifiers: 0,
    activeVerifiers: 0,
    inactiveVerifiers: 0,
    suspendedVerifiers: 0
  });

  loading = signal(false);
  actionLoading = signal(false);
  searchQuery = signal('');
  selectedStatus = signal<string>('ALL');

  // Modals state
  showCreateModal = signal(false);
  showDeactivateModal = signal(false);
  showSuspendModal = signal(false);
  showResetModal = signal(false);
  showDeleteModal = signal(false);

  selectedVerifier = signal<AdminVerifierSummaryDto | null>(null);

  // Forms
  createForm: CreateVerifierRequest = {
    email: '',
    password: '',
    firstName: '',
    lastName: '',
    phone: '',
    mustChangePassword: true
  };

  deactivateReason = '';
  suspendReason = '';
  resetNewPassword = '';
  resetMustChangePassword = true;

  toastMessage = signal<{ text: string; type: 'success' | 'error' } | null>(null);

  ngOnInit(): void {
    this.loadVerifiers();
  }

  loadVerifiers(): void {
    this.loading.set(true);
    const s = this.searchQuery();
    const st = this.selectedStatus();

    this.auth.getVerifiers(s, st, 1, 50).subscribe({
      next: (res) => {
        this.loading.set(false);
        this.verifiers.set(res.items);
        this.stats.set(res.stats);
      },
      error: () => {
        this.loading.set(false);
        // Fallback demo data if backend database is offline
        this.populateFallbackData();
      }
    });
  }

  onSearchChange(): void {
    this.loadVerifiers();
  }

  setStatusFilter(status: string): void {
    this.selectedStatus.set(status);
    this.loadVerifiers();
  }

  // --- Modal Openers ---
  openCreateModal(): void {
    this.createForm = {
      email: '',
      password: this.generateRandomPassword(),
      firstName: '',
      lastName: '',
      phone: '+91 ',
      mustChangePassword: true
    };
    this.showCreateModal.set(true);
  }

  openDeactivateModal(v: AdminVerifierSummaryDto): void {
    this.selectedVerifier.set(v);
    this.deactivateReason = '';
    this.showDeactivateModal.set(true);
  }

  openSuspendModal(v: AdminVerifierSummaryDto): void {
    this.selectedVerifier.set(v);
    this.suspendReason = '';
    this.showSuspendModal.set(true);
  }

  openResetModal(v: AdminVerifierSummaryDto): void {
    this.selectedVerifier.set(v);
    this.resetNewPassword = this.generateRandomPassword();
    this.resetMustChangePassword = true;
    this.showResetModal.set(true);
  }

  openDeleteModal(v: AdminVerifierSummaryDto): void {
    this.selectedVerifier.set(v);
    this.showDeleteModal.set(true);
  }

  closeModals(): void {
    this.showCreateModal.set(false);
    this.showDeactivateModal.set(false);
    this.showSuspendModal.set(false);
    this.showResetModal.set(false);
    this.showDeleteModal.set(false);
    this.selectedVerifier.set(null);
  }

  // --- Actions ---
  submitCreateVerifier(): void {
    if (!this.createForm.email || !this.createForm.password || !this.createForm.firstName || !this.createForm.lastName) {
      this.showToast('Please fill out all required fields.', 'error');
      return;
    }

    this.actionLoading.set(true);
    this.auth.createVerifier(this.createForm).subscribe({
      next: (created) => {
        this.actionLoading.set(false);
        this.closeModals();
        this.showToast(`Verifier account created for ${created.email}`, 'success');
        this.loadVerifiers();
      },
      error: (err) => {
        this.actionLoading.set(false);
        const msg = err.error?.message || 'Failed to create verifier account.';
        this.showToast(msg, 'error');
      }
    });
  }

  submitDeactivate(): void {
    const v = this.selectedVerifier();
    if (!v) return;

    this.actionLoading.set(true);
    this.auth.deactivateVerifier(v.id, this.deactivateReason).subscribe({
      next: () => {
        this.actionLoading.set(false);
        this.closeModals();
        this.showToast(`Verifier ${v.fullName} deactivated.`, 'success');
        this.loadVerifiers();
      },
      error: (err) => {
        this.actionLoading.set(false);
        this.showToast(err.error?.message || 'Action failed.', 'error');
      }
    });
  }

  submitReactivate(v: AdminVerifierSummaryDto): void {
    this.actionLoading.set(true);
    this.auth.reactivateVerifier(v.id).subscribe({
      next: () => {
        this.actionLoading.set(false);
        this.showToast(`Verifier ${v.fullName} reactivated.`, 'success');
        this.loadVerifiers();
      },
      error: (err) => {
        this.actionLoading.set(false);
        this.showToast(err.error?.message || 'Action failed.', 'error');
      }
    });
  }

  submitSuspend(): void {
    const v = this.selectedVerifier();
    if (!v || !this.suspendReason.trim()) {
      this.showToast('Please specify a suspension reason.', 'error');
      return;
    }

    this.actionLoading.set(true);
    this.auth.suspendVerifier(v.id, this.suspendReason).subscribe({
      next: () => {
        this.actionLoading.set(false);
        this.closeModals();
        this.showToast(`Verifier ${v.fullName} has been suspended.`, 'success');
        this.loadVerifiers();
      },
      error: (err) => {
        this.actionLoading.set(false);
        this.showToast(err.error?.message || 'Action failed.', 'error');
      }
    });
  }

  submitResetPassword(): void {
    const v = this.selectedVerifier();
    if (!v || !this.resetNewPassword) {
      this.showToast('Please enter a valid new password.', 'error');
      return;
    }

    this.actionLoading.set(true);
    this.auth.resetVerifierPassword(v.id, this.resetNewPassword, this.resetMustChangePassword).subscribe({
      next: () => {
        this.actionLoading.set(false);
        this.closeModals();
        this.showToast(`Password reset for ${v.fullName}. Session tokens revoked.`, 'success');
        this.loadVerifiers();
      },
      error: (err) => {
        this.actionLoading.set(false);
        this.showToast(err.error?.message || 'Action failed.', 'error');
      }
    });
  }

  submitDelete(): void {
    const v = this.selectedVerifier();
    if (!v) return;

    this.actionLoading.set(true);
    this.auth.deleteVerifier(v.id).subscribe({
      next: () => {
        this.actionLoading.set(false);
        this.closeModals();
        this.showToast(`Verifier ${v.fullName} safely soft-deleted. Audit logs preserved.`, 'success');
        this.loadVerifiers();
      },
      error: (err) => {
        this.actionLoading.set(false);
        this.showToast(err.error?.message || 'Action failed.', 'error');
      }
    });
  }

  generateRandomPassword(): string {
    const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%&*';
    let pass = '';
    for (let i = 0; i < 14; i++) {
      pass += chars.charAt(Math.floor(Math.random() * chars.length));
    }
    return pass + '2026!';
  }

  showToast(text: string, type: 'success' | 'error'): void {
    this.toastMessage.set({ text, type });
    setTimeout(() => {
      this.toastMessage.set(null);
    }, 4500);
  }

  private populateFallbackData(): void {
    const sample: AdminVerifierSummaryDto[] = [
      {
        id: '44444444-0000-0000-0000-000000000001',
        email: 'vikram.iyer@veripass.in',
        firstName: 'Vikram',
        lastName: 'Iyer',
        fullName: 'Vikram Iyer',
        phone: '+91 98112 34567',
        status: 'ACTIVE',
        mustChangePassword: false,
        createdAt: '2026-01-15T10:00:00Z',
        lastLoginAt: '2026-03-05T09:12:00Z',
        assignedCasesCount: 42,
        completedCasesCount: 38,
        pendingCasesCount: 4
      },
      {
        id: '44444444-0000-0000-0000-000000000002',
        email: 'priya.sharma@veripass.in',
        firstName: 'Priya',
        lastName: 'Sharma',
        fullName: 'Priya Sharma',
        phone: '+91 98223 45678',
        status: 'ACTIVE',
        mustChangePassword: false,
        createdAt: '2026-02-01T11:30:00Z',
        lastLoginAt: '2026-03-04T14:45:00Z',
        assignedCasesCount: 29,
        completedCasesCount: 26,
        pendingCasesCount: 3
      },
      {
        id: '44444444-0000-0000-0000-000000000003',
        email: 'arjun.mehta@veripass.in',
        firstName: 'Arjun',
        lastName: 'Mehta',
        fullName: 'Arjun Mehta',
        phone: '+91 98334 56789',
        status: 'SUSPENDED',
        mustChangePassword: true,
        createdAt: '2026-02-18T16:20:00Z',
        lastLoginAt: '2026-02-28T18:00:00Z',
        assignedCasesCount: 14,
        completedCasesCount: 9,
        pendingCasesCount: 5
      }
    ];

    this.verifiers.set(sample);
    this.stats.set({
      totalVerifiers: sample.length,
      activeVerifiers: 2,
      inactiveVerifiers: 0,
      suspendedVerifiers: 1
    });
  }
}
