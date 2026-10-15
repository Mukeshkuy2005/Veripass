import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-buyer-register',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './buyer-register.component.html',
  styleUrl: './buyer-register.component.css'
})
export class BuyerRegisterComponent {
  private router = inject(Router);
  private auth = inject(AuthService);

  form = {
    companyName: '',
    email: '',
    role: 'Procurement Manager',
    orgSize: '1,000+ employees',
    industry: 'Manufacturing',
    password: '',
    terms: true
  };

  loading = signal(false);
  isSuccess = signal(false);
  showPassword = signal(false);

  togglePassword(): void {
    this.showPassword.update(v => !v);
  }

  onSubmit(): void {
    if (!this.form.companyName || !this.form.email || !this.form.password) return;
    this.loading.set(true);

    this.auth.register({
      email: this.form.email.trim(),
      password: this.form.password,
      firstName: this.form.role || 'Procurement',
      lastName: 'Manager',
      organizationName: this.form.companyName.trim(),
      organizationType: 'BUYER',
      phone: ''
    }).subscribe({
      next: () => {
        this.loading.set(false);
        this.isSuccess.set(true);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  goToDashboard(): void {
    this.router.navigate(['/buyer/dashboard']);
  }
}
