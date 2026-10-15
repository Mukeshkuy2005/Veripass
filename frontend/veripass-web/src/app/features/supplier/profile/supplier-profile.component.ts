import { Component, inject, Input, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { BusinessService } from '../../../core/services/business.service';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-supplier-profile',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './supplier-profile.component.html',
  styleUrl: './supplier-profile.component.css'
})
export class SupplierProfileComponent implements OnInit {
  @Input() isEmbedded = false;

  private router = inject(Router);
  private businessService = inject(BusinessService);
  private auth = inject(AuthService);

  loading = signal(false);
  saved = signal(false);

  get businessId(): string {
    return this.auth.currentBusinessId() || this.businessService.currentProfile()?.id || '';
  }

  profileData = {
    businessName: '',
    legalName: '',
    businessType: '',
    industry: '',
    yearEstablished: null as number | null,
    website: '',
    address: '',
    city: '',
    state: '',
    pinCode: ''
  };

  ngOnInit(): void {
    const user = this.auth.currentUser();
    const fallbackName = user?.companyName || user?.organizationName || '';
    const fallbackType = user?.businessType || '';

    // 1. Immediate hydration from active reactive signal if available
    const existing = this.businessService.currentProfile();
    if (existing) {
      this.populateForm(existing, fallbackName, fallbackType);
    }

    // 2. Fetch and synchronize from API service
    const targetId = this.businessId;
    if (targetId) {
      this.businessService.getProfile(targetId).subscribe({
        next: (profile) => {
          if (profile) {
            this.populateForm(profile, fallbackName, fallbackType);
          } else if (!existing) {
            this.profileData.legalName = fallbackName;
            this.profileData.businessName = fallbackName;
            this.profileData.businessType = fallbackType;
          }
        }
      });
    }
  }

  private populateForm(profile: any, fallbackName: string, fallbackType: string): void {
    this.profileData.legalName = profile.legalName || fallbackName;
    this.profileData.businessName = profile.tradeName || profile.legalName || fallbackName;
    this.profileData.businessType = profile.businessType || fallbackType;
    this.profileData.website = profile.website || '';
    this.profileData.industry = profile.industry || '';
    this.profileData.yearEstablished = profile.yearEstablished || null;
    const primaryAddr = profile.addresses?.[0];
    if (primaryAddr) {
      this.profileData.city = primaryAddr.city || '';
      this.profileData.state = primaryAddr.state || '';
      this.profileData.pinCode = primaryAddr.pincode || '';
      this.profileData.address = primaryAddr.line1 || '';
    } else {
      this.profileData.city = '';
      this.profileData.state = '';
      this.profileData.pinCode = '';
      this.profileData.address = '';
    }
  }

  saveAndContinue(): void {
    this.loading.set(true);

    const updatedAddresses = (this.profileData.address || this.profileData.city) ? [{
      id: 'addr-1',
      addressType: 'REGISTERED',
      line1: this.profileData.address,
      city: this.profileData.city,
      state: this.profileData.state,
      pincode: this.profileData.pinCode,
      country: 'India',
      isPrimary: true,
      verificationStatus: 'VERIFIED'
    }] : [];

    this.businessService.updateProfile({
      legalName: this.profileData.legalName.trim(),
      tradeName: this.profileData.businessName.trim() || this.profileData.legalName.trim(),
      businessType: this.profileData.businessType,
      website: this.profileData.website?.trim(),
      addresses: updatedAddresses,
      ...({
        industry: this.profileData.industry,
        yearEstablished: this.profileData.yearEstablished
      } as any)
    }).subscribe({
      next: () => {
        this.loading.set(false);
        this.saved.set(true);
        if (this.isEmbedded) {
          setTimeout(() => this.saved.set(false), 3500);
        } else {
          setTimeout(() => {
            this.router.navigate(['/supplier/settings'], { queryParams: { tab: 'identity-kyc' } });
          }, 700);
        }
      },
      error: () => {
        this.loading.set(false);
        this.saved.set(true);
        if (this.isEmbedded) {
          setTimeout(() => this.saved.set(false), 3500);
        } else {
          setTimeout(() => {
            this.router.navigate(['/supplier/settings'], { queryParams: { tab: 'identity-kyc' } });
          }, 700);
        }
      }
    });
  }
}
