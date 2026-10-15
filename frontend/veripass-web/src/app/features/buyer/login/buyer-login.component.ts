import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-buyer-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './buyer-login.component.html',
  styleUrl: './buyer-login.component.css'
})
export class BuyerLoginComponent {
  private router = inject(Router);
  private auth = inject(AuthService);

  email = '';
  password = '';
  rememberMe = true;
  loading = signal(false);
  showPassword = signal(false);
  errorMessage = signal<string | null>(null);

  togglePassword(): void {
    this.showPassword.update(v => !v);
  }

  onSubmit(): void {
    if (!this.email || !this.password) return;
    this.loading.set(true);
    this.errorMessage.set(null);

    this.auth.login({
      email: this.email.trim(),
      password: this.password
    }).subscribe({
      next: () => {
        this.loading.set(false);
        this.router.navigate(['/buyer/dashboard']);
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err.error?.message || err.message || 'Invalid email or password.');
      }
    });
  }
}
