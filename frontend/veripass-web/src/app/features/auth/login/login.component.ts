import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';

export type LoginPersona = 'BUYER' | 'SUPPLIER' | 'ADMIN';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css'
})
export class LoginComponent implements OnInit {
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private auth = inject(AuthService);

  email = '';
  password = '';
  rememberDevice = true;
  showPassword = signal(false);
  loading = signal(false);
  errorMessage = signal<string | null>(null);
  mfaRequired = signal(false);
  mfaTicket?: string;

  selectedPersona = signal<LoginPersona>('BUYER');
  capsLockOn = signal(false);

  otpDigits = ['', '', '', '', '', ''];
  resendCountdown = signal(30);

  // Live interactive feedback signals
  isValidEmail = () => {
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(this.email.trim());
  };

  getRecognizedDomain = () => {
    const parts = this.email.trim().split('@');
    if (parts.length === 2 && parts[1].includes('.')) {
      return parts[1].toLowerCase();
    }
    return null;
  };

  ngOnInit(): void {
    const currentUrl = this.router.url.toLowerCase();
    if (currentUrl.includes('buyer')) {
      this.selectedPersona.set('BUYER');
    } else if (currentUrl.includes('supplier')) {
      this.selectedPersona.set('SUPPLIER');
    } else if (currentUrl.includes('admin') || currentUrl.includes('verifier')) {
      this.selectedPersona.set('ADMIN');
    }

    const qEmail = this.route.snapshot.queryParams['email'];
    if (qEmail) {
      this.email = qEmail.trim();
    }

    const qRole = this.route.snapshot.queryParams['role'];
    if (qRole) {
      const upper = qRole.toUpperCase();
      if (upper === 'SUPPLIER' || upper === 'BUSINESS') this.selectedPersona.set('SUPPLIER');
      else if (upper === 'ADMIN') this.selectedPersona.set('ADMIN');
      else if (upper === 'BUYER') this.selectedPersona.set('BUYER');
    }
  }

  selectPersona(persona: LoginPersona): void {
    this.selectedPersona.set(persona);
    this.errorMessage.set(null);
  }

  checkCapsLock(event: KeyboardEvent): void {
    if (event.getModifierState) {
      this.capsLockOn.set(event.getModifierState('CapsLock'));
    }
  }

  handleInitialSignIn(): void {
    if (!this.email || !this.password) return;

    this.loading.set(true);
    this.errorMessage.set(null);

    this.auth.login({ email: this.email.trim(), password: this.password }).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res.requiresMfa) {
          this.mfaRequired.set(true);
          this.mfaTicket = res.mfaTicket;
          this.otpDigits = ['', '', '', '', '', ''];
          this.startCountdown();
          return;
        }
        if (res.mustChangePassword) {
          this.router.navigate(['/auth/change-password']);
          return;
        }
        this.routeByRole(res.role);
      },
      error: (err) => {
        this.loading.set(false);
        let msg = err.error?.message || err.error?.detail || err.message || 'Invalid email or password.';
        if (err.status === 0) {
          msg = 'Unable to connect to the backend server (http://localhost:5000). Please ensure the backend is running.';
        }
        this.errorMessage.set(msg);
      }
    });
  }

  continueWithEmail(): void {
    this.handleInitialSignIn();
  }

  onOtpInput(event: any, index: number): void {
    const input = event.target as HTMLInputElement;
    const value = input.value;
    if (value && index < 5) {
      const nextInput = document.getElementById(`otp-${index + 1}`) as HTMLInputElement;
      if (nextInput) nextInput.focus();
    }
  }

  onOtpBackspace(event: any, index: number): void {
    if (!this.otpDigits[index] && index > 0) {
      const prevInput = document.getElementById(`otp-${index - 1}`) as HTMLInputElement;
      if (prevInput) prevInput.focus();
    }
  }

  onOtpPaste(event: ClipboardEvent): void {
    event.preventDefault();
    const pasteData = event.clipboardData?.getData('text') || '';
    const digits = pasteData.replace(/\D/g, '').slice(0, 6);
    if (!digits) return;

    for (let i = 0; i < 6; i++) {
      this.otpDigits[i] = digits[i] || '';
    }

    const focusIndex = Math.min(digits.length, 5);
    const targetInput = document.getElementById(`otp-${focusIndex}`) as HTMLInputElement;
    if (targetInput) targetInput.focus();
  }

  isOtpComplete(): boolean {
    return this.otpDigits.every(d => d.trim().length === 1);
  }

  verifyOtp(): void {
    if (!this.isOtpComplete()) return;

    this.loading.set(true);
    this.errorMessage.set(null);
    const code = this.otpDigits.join('');

    this.auth.verifyMfaLogin({
      email: this.email.trim(),
      code: code,
      ticket: this.mfaTicket
    }).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res.mustChangePassword) {
          this.router.navigate(['/auth/change-password']);
          return;
        }
        this.routeByRole(res.role);
      },
      error: (err) => {
        this.loading.set(false);
        const msg = err.error?.message || err.message || 'Invalid or expired verification code. Please check your Gmail or request a new code.';
        this.errorMessage.set(msg);
      }
    });
  }

  resendOtp(): void {
    if (this.resendCountdown() > 0) return;
    this.resendCountdown.set(30);
    this.startCountdown();
    this.errorMessage.set(null);

    this.auth.resendMfaOtp(this.email.trim()).subscribe({
      next: () => {
        // Otp sent
      },
      error: (err) => {
        const msg = err.error?.message || 'Failed to resend code to Gmail.';
        this.errorMessage.set(msg);
      }
    });
  }

  startCountdown(): void {
    this.resendCountdown.set(30);
    const timer = setInterval(() => {
      if (this.resendCountdown() > 0) {
        this.resendCountdown.update(c => c - 1);
      } else {
        clearInterval(timer);
      }
    }, 1000);
  }

  routeByRole(role: string): void {
    const upper = (role || 'SUPPLIER').toUpperCase();
    if (upper === 'ADMIN') this.router.navigate(['/admin/dashboard']);
    else if (upper === 'VERIFIER') this.router.navigate(['/verifier/dashboard']);
    else if (upper === 'BUYER') this.router.navigate(['/buyer/dashboard']);
    else this.router.navigate(['/supplier/dashboard']);
  }
}

