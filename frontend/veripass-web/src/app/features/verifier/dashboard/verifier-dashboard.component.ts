import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { VerifierDashboardService } from './services/verifier-dashboard.service';
import { VerificationKpiCardsComponent } from './components/verification-kpi-cards/verification-kpi-cards.component';
import { PriorityCasesComponent } from './components/priority-cases/priority-cases.component';
import { VerificationPerformanceComponent } from './components/verification-performance/verification-performance.component';
import { RiskDistributionComponent } from './components/risk-distribution/risk-distribution.component';
import { RecentDecisionsComponent } from './components/recent-decisions/recent-decisions.component';

@Component({
  selector: 'app-verifier-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    VerificationKpiCardsComponent,
    PriorityCasesComponent,
    VerificationPerformanceComponent,
    RiskDistributionComponent,
    RecentDecisionsComponent
  ],
  templateUrl: './verifier-dashboard.component.html',
  styleUrl: './verifier-dashboard.component.css'
})
export class VerifierDashboardComponent implements OnInit {
  protected dashboardService = inject(VerifierDashboardService);

  currentDate = new Date();

  // Signals exposed to template
  summary = this.dashboardService.summary;
  priorityCases = this.dashboardService.priorityCases;
  performance = this.dashboardService.performance;
  riskDistribution = this.dashboardService.riskDistribution;
  recentDecisions = this.dashboardService.recentDecisions;

  isLoading = this.dashboardService.isLoading;
  isRefreshing = this.dashboardService.isRefreshing;
  error = this.dashboardService.error;
  refreshError = this.dashboardService.refreshError;
  lastRefreshedAt = this.dashboardService.lastRefreshedAt;
  hasData = this.dashboardService.hasData;

  ngOnInit(): void {
    this.dashboardService.loadDashboard().subscribe({
      error: () => {
        // Handled in service signal
      }
    });
  }

  onRefresh(): void {
    this.currentDate = new Date();
    this.dashboardService.refreshDashboard().subscribe({
      error: () => {
        // Handled in service signal
      }
    });
  }

  onRetry(): void {
    this.onRefresh();
  }

  dismissRefreshError(): void {
    this.dashboardService.clearRefreshError();
  }
}
