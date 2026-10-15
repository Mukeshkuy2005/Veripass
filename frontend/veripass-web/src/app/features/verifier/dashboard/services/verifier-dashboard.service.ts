import { Injectable, inject, signal, computed } from '@angular/core';
import { Observable, tap, catchError, throwError } from 'rxjs';
import { ApiService } from '../../../../core/services/api.service';
import { VerifierDashboardResponse } from '../models/verifier-dashboard.model';

@Injectable({
  providedIn: 'root'
})
export class VerifierDashboardService {
  private api = inject(ApiService);

  // Reactive state signals
  dashboardData = signal<VerifierDashboardResponse | null>(null);
  isLoading = signal<boolean>(false);
  isRefreshing = signal<boolean>(false);
  error = signal<string | null>(null);
  refreshError = signal<string | null>(null);
  lastRefreshedAt = signal<Date | null>(null);

  // Computed state selectors
  summary = computed(() => this.dashboardData()?.summary ?? null);
  priorityCases = computed(() => this.dashboardData()?.priorityCases ?? []);
  performance = computed(() => this.dashboardData()?.performance ?? null);
  riskDistribution = computed(() => this.dashboardData()?.riskDistribution ?? null);
  recentDecisions = computed(() => this.dashboardData()?.recentDecisions ?? []);

  hasData = computed(() => this.dashboardData() !== null);

  /**
   * Fetch dashboard data from PostgreSQL-backed API.
   * If existing data is present, preserves data and handles non-blocking refresh errors.
   */
  loadDashboard(): Observable<VerifierDashboardResponse> {
    const isInitial = !this.hasData();
    if (isInitial) {
      this.isLoading.set(true);
      this.error.set(null);
    } else {
      this.isRefreshing.set(true);
      this.refreshError.set(null);
    }

    return this.api.get<VerifierDashboardResponse>('verifier/dashboard').pipe(
      tap({
        next: (response) => {
          this.dashboardData.set(response);
          this.lastRefreshedAt.set(new Date());
          this.error.set(null);
          this.refreshError.set(null);
          this.isLoading.set(false);
          this.isRefreshing.set(false);
        },
        error: (err) => {
          const errorMessage = err?.error?.message || err?.message || 'Unable to connect to verification telemetry service.';
          if (isInitial) {
            this.error.set(errorMessage);
          } else {
            this.refreshError.set(`Couldn't refresh dashboard data. Showing cached metrics from ${this.formatTime(this.lastRefreshedAt())}.`);
          }
          this.isLoading.set(false);
          this.isRefreshing.set(false);
        }
      })
    );
  }

  refreshDashboard(): Observable<VerifierDashboardResponse> {
    return this.loadDashboard();
  }

  clearRefreshError(): void {
    this.refreshError.set(null);
  }

  private formatTime(date: Date | null): string {
    if (!date) return 'earlier';
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }
}
