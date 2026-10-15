import { Injectable, inject, signal } from '@angular/core';
import { ApiService } from './api.service';
import { AuthService } from '../auth/auth.service';
import { catchError, finalize, of, tap } from 'rxjs';

export interface PillarCompliance {
  identityPercent: number;
  taxPercent: number;
  addressPercent: number;
  bankPercent: number;
  certificationsPercent: number;
}

export interface DashboardActionItem {
  id: string;
  type: string;
  title: string;
  description: string;
  count: number;
  actionLabel: string;
  actionUrl: string;
  severity: 'danger' | 'amber' | 'blue';
}

export interface DashboardActivity {
  time: string;
  title: string;
  description: string;
  statusColor: 'green' | 'blue' | 'amber' | 'red';
}

export interface BuyerDashboardData {
  organizationId: string;
  name: string;
  type: string;
  totalMembers: number;
  totalSuppliers: number;
  verifiedSuppliers: number;
  pendingSuppliers: number;
  expiredSuppliers: number;
  highRiskSuppliers: number;
  verifiedPercentage: number;
  pendingPercentage: number;
  expiredPercentage: number;
  highRiskPercentage: number;
  itcSafeguarded: string;
  averageTrustScore: number;
  pillarCompliance: PillarCompliance;
  actionRequired: DashboardActionItem[];
  recentActivities: DashboardActivity[];
}

@Injectable({
  providedIn: 'root'
})
export class BuyerDashboardService {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  public dashboardData = signal<BuyerDashboardData | null>(null);
  public loading = signal<boolean>(false);
  public error = signal<string | null>(null);

  constructor() {
    this.loadDashboard();
  }

  public loadDashboard(orgId?: string): void {
    this.loading.set(true);
    this.error.set(null);

    const currentUser = this.auth.currentUser();
    const targetOrgId = orgId || currentUser?.currentOrganizationId;

    if (!targetOrgId) {
      this.dashboardData.set(this.getEmptyDashboard());
      this.loading.set(false);
      return;
    }

    this.api.get<BuyerDashboardData>(`organizations/${targetOrgId}/dashboard`)
      .pipe(
        tap(data => {
          this.dashboardData.set(data);
          this.loading.set(false);
        }),
        catchError(err => {
          console.warn('[BuyerDashboardService] Failed to load dashboard from API:', err);
          this.dashboardData.set(this.getEmptyDashboard(targetOrgId, currentUser?.companyName || currentUser?.organizationName));
          this.loading.set(false);
          return of(null);
        }),
        finalize(() => {
          this.loading.set(false);
        })
      )
      .subscribe();
  }

  public refreshDashboard(): void {
    this.loadDashboard();
  }

  private getEmptyDashboard(orgId?: string, orgName?: string): BuyerDashboardData {
    const user = this.auth.currentUser();
    return {
      organizationId: orgId || user?.currentOrganizationId || '',
      name: orgName || user?.companyName || user?.organizationName || 'Enterprise Buyer Workspace',
      type: 'BUYER',
      totalMembers: 1,
      totalSuppliers: 0,
      verifiedSuppliers: 0,
      pendingSuppliers: 0,
      expiredSuppliers: 0,
      highRiskSuppliers: 0,
      verifiedPercentage: 0,
      pendingPercentage: 0,
      expiredPercentage: 0,
      highRiskPercentage: 0,
      itcSafeguarded: '₹0 Safeguarded',
      averageTrustScore: 0,
      pillarCompliance: {
        identityPercent: 0,
        taxPercent: 0,
        addressPercent: 0,
        bankPercent: 0,
        certificationsPercent: 0
      },
      actionRequired: [],
      recentActivities: []
    };
  }
}
