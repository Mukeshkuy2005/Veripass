import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { BusinessService } from '../../../core/services/business.service';

export type UserRoleChoice = 'BUSINESS' | 'BUYER';

export interface RoleDefinition {
  id: UserRoleChoice;
  internalRole: 'SUPPLIER' | 'BUYER';
  title: string;
  badge: string;
  icon: string;
  subtitle: string;
  targetInterface: string;
  targetRoute: string;
  clarification: string;
}

@Component({
  selector: 'app-supplier-register',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './supplier-register.component.html',
  styleUrl: './supplier-register.component.css'
})
export class SupplierRegisterComponent implements OnInit, OnDestroy {
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private auth = inject(AuthService);
  private businessService = inject(BusinessService);

  currentStep = signal(1);
  loading = signal(false);
  accountCreated = signal(false);
  errorMessage = signal<string | null>(null);
  redirectCountdown = signal(3);
  private countdownTimer: any = null;

  selectedRole = signal<UserRoleChoice>('BUSINESS');

  readonly roles: RoleDefinition[] = [
    {
      id: 'BUSINESS',
      internalRole: 'SUPPLIER',
      title: 'Business (Supplier)',
      badge: 'SUPPLIER WORKSPACE',
      icon: '🏭',
      subtitle: 'For Indian manufacturers, vendors & suppliers registering business identity & credentials',
      targetInterface: 'Supplier Interface',
      targetRoute: '/supplier/dashboard',
      clarification: 'Registered businesses manage GSTIN, PAN, credentials, and their sovereign verifiable Passport through the Supplier Interface.'
    },
    {
      id: 'BUYER',
      internalRole: 'BUYER',
      title: 'Enterprise Buyer',
      badge: 'BUYER WORKSPACE',
      icon: '🏢',
      subtitle: 'For corporate procurement teams discovering, comparing, and managing verified suppliers',
      targetInterface: 'Buyer Interface',
      targetRoute: '/buyer/dashboard',
      clarification: 'Enterprise procurement accounts access supplier discovery, vendor risk comparison, and onboarding pipelines.'
    }
  ];

  activeRoleDef = computed(() => {
    return this.roles.find(r => r.id === this.selectedRole()) || this.roles[0];
  });

  showPassword = signal(false);
  capsLockOn = signal(false);

  passwordStrength = computed(() => {
    const pwd = this.formData.password || '';
    if (!pwd) return { score: 0, label: '', percent: 0, color: '#E2E8F0' };
    let score = 0;
    if (pwd.length >= 8) score++;
    if (/[a-zA-Z]/.test(pwd)) score++;
    if (/\d/.test(pwd)) score++;
    if (/[^a-zA-Z0-9]/.test(pwd)) score++;

    if (score <= 1) return { score: 1, label: 'Weak', percent: 25, color: '#EF4444' };
    if (score === 2) return { score: 2, label: 'Fair', percent: 50, color: '#F59E0B' };
    if (score === 3) return { score: 3, label: 'Good', percent: 75, color: '#3B82F6' };
    return { score: 4, label: 'Strong', percent: 100, color: '#10B981' };
  });

  isValidEmail(): boolean {
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(this.formData.email.trim());
  }

  isValidPhone(): boolean {
    const clean = (this.formData.mobileNumber || '').replace(/\D/g, '');
    return clean.length === 10;
  }

  checkCapsLock(event: KeyboardEvent): void {
    if (event.getModifierState) {
      this.capsLockOn.set(event.getModifierState('CapsLock'));
    }
  }

  formData = {
    email: '',
    mobileNumber: '',
    password: '',
    fullName: '',
    entityName: '',
    entityType: '',
    acceptTerms: false
  };

  ngOnInit(): void {
    // 1. Path-based detection for dedicated route aliases
    const currentPath = this.router.url.toLowerCase();
    if (currentPath.includes('buyer')) {
      this.selectedRole.set('BUYER');
    } else {
      this.selectedRole.set('BUSINESS');
    }

    // 2. Query parameter detection takes precedence (?role=business|buyer)
    this.route.queryParamMap.subscribe(params => {
      const roleParam = (params.get('role') || '').toUpperCase();
      if (roleParam === 'BUYER') {
        this.selectedRole.set('BUYER');
      } else if (roleParam === 'BUSINESS' || roleParam === 'SUPPLIER') {
        this.selectedRole.set('BUSINESS');
      }
    });
  }

  ngOnDestroy(): void {
    if (this.countdownTimer) {
      clearInterval(this.countdownTimer);
    }
  }

  selectRole(role: UserRoleChoice): void {
    this.selectedRole.set(role);
    this.formData.entityType = '';
  }

  isFormValid(): boolean {
    return !!(
      this.formData.email?.trim() &&
      this.formData.mobileNumber?.trim() &&
      this.formData.password &&
      this.formData.fullName?.trim() &&
      this.formData.entityName?.trim() &&
      this.formData.entityType &&
      this.formData.acceptTerms
    );
  }

  handleCreateAccount(): void {
    if (!this.isFormValid()) return;

    this.loading.set(true);
    this.errorMessage.set(null);

    const targetDef = this.activeRoleDef();
    const cleanEmail = this.formData.email.trim();
    const cleanName = this.formData.fullName.trim();
    const cleanEntity = this.formData.entityName.trim();
    const cleanPhone = this.formData.mobileNumber.trim();
    const cleanType = this.formData.entityType;

    const parts = (cleanName || 'User').split(' ');
    const firstName = parts[0] || 'User';
    const lastName = parts.slice(1).join(' ') || (targetDef.internalRole === 'BUYER' ? 'Buyer' : 'Supplier');

    // Register with backend PostgreSQL database
    this.auth.register({
      email: cleanEmail,
      password: this.formData.password,
      firstName: firstName,
      lastName: lastName,
      organizationName: cleanEntity,
      organizationType: targetDef.internalRole === 'BUYER' ? 'BUYER' : 'SUPPLIER',
      phone: cleanPhone
    }).subscribe({
      next: (res) => {
        // Initialize fresh, unverified business profile for supplier
        if (targetDef.internalRole === 'SUPPLIER') {
          this.businessService.initializeNewBusiness({
            companyName: cleanEntity,
            entityType: cleanType,
            contactPerson: cleanName,
            email: cleanEmail,
            phone: cleanPhone
          });
        }

        this.loading.set(false);
        this.accountCreated.set(true);
        this.currentStep.set(2);
        this.startRedirectCountdown();
      },
      error: (err) => {
        this.loading.set(false);
        const msg = err.error?.message || (typeof err.error === 'string' ? err.error : null) || 'Registration failed. Please verify your details or use a different email.';
        this.errorMessage.set(msg);
      }
    });
  }

  startRedirectCountdown(): void {
    this.redirectCountdown.set(3);
    this.countdownTimer = setInterval(() => {
      const current = this.redirectCountdown();
      if (current > 1) {
        this.redirectCountdown.set(current - 1);
      } else {
        clearInterval(this.countdownTimer);
        this.proceedToTargetWorkspace();
      }
    }, 1000);
  }

  proceedToTargetWorkspace(): void {
    if (this.countdownTimer) {
      clearInterval(this.countdownTimer);
    }
    const targetRoute = this.activeRoleDef().targetRoute;
    this.router.navigate([targetRoute]);
  }
}

// Export alias for clean standalone naming
export { SupplierRegisterComponent as RegisterComponent };
