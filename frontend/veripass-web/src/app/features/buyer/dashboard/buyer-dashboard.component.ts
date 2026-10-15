import { Component, computed, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { BuyerSearchService } from '../../../core/services/buyer-search.service';
import { BuyerDashboardService } from '../../../core/services/buyer-dashboard.service';

@Component({
  selector: 'app-buyer-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './buyer-dashboard.component.html',
  styleUrl: './buyer-dashboard.component.css'
})
export class BuyerDashboardComponent implements OnInit {
  public searchService = inject(BuyerSearchService);
  public dashboardService = inject(BuyerDashboardService);
  private auth = inject(AuthService);
  private router = inject(Router);

  user = this.auth.currentUser;
  data = this.dashboardService.dashboardData;
  isLoading = this.dashboardService.loading;

  companyName = computed(() => {
    return this.data()?.name || this.user()?.companyName || this.user()?.organizationName || 'Buyer Organization';
  });

  todayDateStr = computed(() => {
    const d = new Date();
    return d.toLocaleDateString('en-IN', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });
  });

  ngOnInit(): void {
    this.dashboardService.loadDashboard();
  }

  refreshTelemetry(): void {
    this.dashboardService.refreshDashboard();
  }

  filterSuppliers(filterType: 'ALL' | 'VERIFIED' | 'PENDING' | 'EXPIRED' | 'HIGH_RISK'): void {
    switch (filterType) {
      case 'ALL':
        this.router.navigate(['/buyer/suppliers']);
        break;
      case 'VERIFIED':
        this.router.navigate(['/buyer/suppliers'], { queryParams: { status: 'APPROVED' } });
        break;
      case 'PENDING':
        this.router.navigate(['/buyer/suppliers'], { queryParams: { status: 'PENDING' } });
        break;
      case 'EXPIRED':
        this.router.navigate(['/buyer/suppliers'], { queryParams: { status: 'SUSPENDED' } });
        break;
      case 'HIGH_RISK':
        this.router.navigate(['/buyer/suppliers'], { queryParams: { risk: 'HIGH' } });
        break;
    }
  }
}
