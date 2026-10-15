import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-change-password',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './change-password.component.html',
  styleUrl: './change-password.component.css'
})
export class ChangePasswordComponent implements OnInit {
  private router = inject(Router);
  private auth = inject(AuthService);

  currentPassword = '';
  newPassword = '';
  confirmPassword = '';

  loading = signal(false);
  errorMessage = signal<string | null>(null);
  successMessage = signal<string | null>(null);
  isForced = signal(false);

  showCurrent = signal(false);
  showNew = signal(false);
  showConfirm = signal(false);

  ngOnInit(): void {
    const user = this.auth.currentUser();
    if (!user) {
      this.router.navigate(['/auth/login']);
      return;
    }
    this.isForced.set(!!user.mustChangePassword);
  }

  get hasMinLength(): boolean {
    return this.newPassword.length >= 8;
  }

  get hasNumberOrSymbol(): boolean {
    return /[0-9!@#$%^&*(),.?":{}|<>]/.test(this.newPassword);
  }

  get passwordsMatch(): boolean {
    return !!this.newPassword && this.newPassword === this.confirmPassword;
  }

  get isFormValid(): boolean {
    if (this.isForced()) {
      return this.hasMinLength && this.passwordsMatch;
    }
    return !!this.currentPassword && this.hasMinLength && this.passwordsMatch;
  }

  handleSubmit(): void {
    if (!this.isFormValid) return;

    this.loading.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    const currentPass = this.currentPassword ? this.currentPassword.trim() : null;
    const newPass = this.newPassword.trim();

    this.auth.changePassword(currentPass, newPass).subscribe({
      next: (res) => {
        this.loading.set(false);
        this.successMessage.set(res?.message || 'Your password has been changed successfully.');
        setTimeout(() => {
          this.navigateToWorkspace();
        }, 1500);
      },
      error: (err) => {
        this.loading.set(false);
        const msg = err.error?.message || err.message || 'Failed to change password. Please check your current password.';
        this.errorMessage.set(msg);
      }
    });
  }

  navigateToWorkspace(): void {
    const role = (this.auth.currentRole() || 'SUPPLIER').toUpperCase();
    if (role === 'ADMIN') this.router.navigate(['/admin/dashboard']);
    else if (role === 'VERIFIER') this.router.navigate(['/verifier/dashboard']);
    else if (role === 'BUYER') this.router.navigate(['/buyer/dashboard']);
    else this.router.navigate(['/supplier/dashboard']);
  }
}
