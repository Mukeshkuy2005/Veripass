import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { BusinessService } from '../../../core/services/business.service';
import { AuthService } from '../../../core/auth/auth.service';
import {
  BusinessDashboardDto,
  BusinessPassportDto,
  BusinessProfileDto
} from '../../../core/models/models';

import { BusinessStatusHeroComponent } from './components/business-status-hero/business-status-hero.component';
import { CoreMetricsGridComponent } from './components/core-metrics-grid/core-metrics-grid.component';
import { ActionRequiredListComponent, ActionRequiredItem } from './components/action-required-list/action-required-list.component';
import { VerificationOverviewComponent, VerificationCheckItem } from './components/verification-overview/verification-overview.component';
import { ComplianceHealthComponent, ComplianceTrendPoint } from './components/compliance-health/compliance-health.component';
import { UpcomingExpirationsComponent, UpcomingExpirationItem } from './components/upcoming-expirations/upcoming-expirations.component';
import { RecentActivityTimelineComponent, RecentActivityItem } from './components/recent-activity-timeline/recent-activity-timeline.component';
import { PassportQuickAccessComponent } from './components/passport-quick-access/passport-quick-access.component';
import { formatActivityTime } from '../../../core/utils/ist-date-formatter';

@Component({
  selector: 'app-supplier-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    BusinessStatusHeroComponent,
    CoreMetricsGridComponent,
    ActionRequiredListComponent,
    VerificationOverviewComponent,
    ComplianceHealthComponent,
    UpcomingExpirationsComponent,
    RecentActivityTimelineComponent,
    PassportQuickAccessComponent
  ],
  templateUrl: './supplier-dashboard.component.html',
  styleUrl: './supplier-dashboard.component.css'
})
export class SupplierDashboardComponent implements OnInit {
  public businessService = inject(BusinessService);
  private auth = inject(AuthService);

  loading = signal<boolean>(true);
  errorMessage = signal<string | null>(null);

  dashboardData = signal<BusinessDashboardDto | null>(null);
  profileData = signal<BusinessProfileDto | null>(null);
  passportData = signal<BusinessPassportDto | null>(null);

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    const businessId = this.auth.currentBusinessId();

    this.businessService.getDashboard(businessId).subscribe({
      next: (dash) => {
        this.dashboardData.set(dash);
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set('Unable to retrieve latest verification metrics.');
        this.loading.set(false);
      }
    });

    this.businessService.getProfile(businessId).subscribe({
      next: (prof) => this.profileData.set(prof),
      error: () => {}
    });

    this.businessService.getPassport(businessId).subscribe({
      next: (pass) => {
        this.passportData.set(pass);
        if (pass?.verificationScore) {
          const current = this.businessService.currentProfile();
          if (current) {
            this.businessService.saveProfileLocally({
              ...current,
              trustScore: pass.verificationScore
            });
          }
        }
      },
      error: () => {}
    });
  }

  get businessName(): string {
    return this.profileData()?.legalName
      || this.profileData()?.tradeName
      || this.dashboardData()?.businessName
      || this.auth.currentUser()?.companyName
      || this.auth.currentUser()?.organizationName
      || 'My Business';
  }

  get gstin(): string {
    return this.profileData()?.gstin || '';
  }

  get cin(): string {
    return this.profileData()?.cin || '';
  }

  get verificationStatus(): string {
    return this.profileData()?.verificationStatus || this.dashboardData()?.verificationStatus || 'UNVERIFIED';
  }

  get verificationScore(): number {
    return this.passportData()?.verificationScore
      ?? this.dashboardData()?.overallTrustScore
      ?? this.profileData()?.trustScore
      ?? 0;
  }

  get complianceScore(): number {
    return this.dashboardData()?.complianceScore || 0;
  }

  get totalCredentials(): number {
    return this.dashboardData()?.totalCredentials || 0;
  }

  get verifiedCredentials(): number {
    return this.dashboardData()?.verifiedCredentials || 0;
  }

  get passportId(): string {
    return this.passportData()?.passportNumber
      || this.profileData()?.businessIdNumber
      || this.dashboardData()?.businessIdNumber
      || 'VP-IN-PENDING';
  }

  get actionRequiredItems(): ActionRequiredItem[] {
    if (this.verificationStatus !== 'VERIFIED') {
      return [
        {
          id: 'act-1',
          title: 'Complete Business Profile details',
          subtitle: 'Fill in principal operating address, industry sector and trade details',
          urgency: 'warning',
          actionLabel: 'Complete Profile →',
          routerLink: '/supplier/settings',
          queryParams: { tab: 'business-profile' }
        },
        {
          id: 'act-2',
          title: 'Verify GSTIN & PAN Identity',
          subtitle: 'Submit statutory tax credentials for automated MCA & GSTN matching',
          urgency: 'warning',
          actionLabel: 'Verify Identity →',
          routerLink: '/supplier/settings',
          queryParams: { tab: 'identity-kyc' }
        },
        {
          id: 'act-3',
          title: 'Upload Statutory Business Documents',
          subtitle: 'Upload registration certificates, Udyam MSME, and quality permits',
          urgency: 'info',
          actionLabel: 'Upload Documents →',
          routerLink: '/supplier/documents'
        }
      ];
    }
    return [];
  }

  get verificationChecks(): VerificationCheckItem[] {
    const isVerified = this.verificationStatus === 'VERIFIED';
    return [
      { id: 'c1', name: 'Identity (CIN & MCA)', category: 'Corporate', status: isVerified ? 'VERIFIED' : 'PENDING', lastChecked: isVerified ? 'Recent' : 'Pending setup' },
      { id: 'c2', name: 'GSTIN Registration', category: 'Taxation', status: isVerified ? 'VERIFIED' : 'PENDING', lastChecked: isVerified ? 'Recent' : 'Pending setup' },
      { id: 'c3', name: 'PAN Corporate Entity', category: 'Taxation', status: isVerified ? 'VERIFIED' : 'PENDING', lastChecked: isVerified ? 'Recent' : 'Pending setup' },
      { id: 'c4', name: 'Business Address (REG-06)', category: 'Location', status: isVerified ? 'VERIFIED' : 'PENDING', lastChecked: isVerified ? 'Recent' : 'Pending setup' },
      { id: 'c5', name: 'Bank Account Penny-Drop', category: 'Banking', status: isVerified ? 'VERIFIED' : 'PENDING', lastChecked: isVerified ? 'Recent' : 'Pending setup' },
      { id: 'c6', name: 'Certifications (ISO / MSME)', category: 'Quality', status: isVerified ? 'VERIFIED' : 'PENDING', lastChecked: isVerified ? 'Recent' : 'Pending setup' }
    ];
  }

  get verifiedPills(): string[] {
    const creds = this.passportData()?.verifiedCredentials;
    if (creds && creds.length > 0) {
      return creds.map(c => c.credentialType || c.credentialName);
    }
    return [];
  }

  get recentActivities(): RecentActivityItem[] {
    const raw = this.dashboardData()?.recentActivities;
    if (raw && raw.length > 0) {
      return raw.map((r, i) => {
        const timeDisplay = formatActivityTime(r.timestamp);
        return {
          id: `act-${i}`,
          date: timeDisplay.relativeOrDate,
          fullDate: timeDisplay.exactDateTime,
          title: r.title,
          subtitle: r.description,
          status: (r.type === 'VERIFIED' ? 'VERIFIED' : r.type === 'ALERT' ? 'ACTION' : 'IN_REVIEW') as any,
          tag: r.type
        };
      });
    }
    return [];
  }
}
