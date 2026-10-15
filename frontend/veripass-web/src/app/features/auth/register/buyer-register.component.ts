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
  auth = inject(AuthService);
  router = inject(Router);

  submitting = signal(false);
  errorMessage = signal<string | null>(null);

  form = {
    companyName: '',
    gstin: '',
    industry: '',
    spendTier: '',
    vendorCount: '',
    fullName: '',
    email: '',
    password: '',
    phone: ''
  };

  onSubmit(): void {
    if (!this.form.email || !this.form.password || !this.form.companyName) {
      this.errorMessage.set('Please fill in all required fields.');
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    const parts = (this.form.fullName || 'User').trim().split(' ');
    const firstName = parts[0] || 'User';
    const lastName = parts.slice(1).join(' ') || 'Buyer';

    this.auth.register({
      email: this.form.email.trim(),
      password: this.form.password,
      firstName: firstName,
      lastName: lastName,
      organizationName: this.form.companyName.trim(),
      organizationType: 'BUYER',
      phone: this.form.phone.trim()
    }).subscribe({
      next: (res) => {
        this.submitting.set(false);
        this.router.navigateByUrl('/buyer/dashboard');
      },
      error: (err) => {
        this.submitting.set(false);
        const msg = err.error?.message || err.message || 'Registration failed. Please try again.';
        this.errorMessage.set(msg);
      }
    });
  }
}

