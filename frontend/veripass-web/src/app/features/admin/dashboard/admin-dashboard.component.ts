import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AdminStateService } from '../../../core/services/admin-state.service';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './admin-dashboard.component.html',
  styleUrl: './admin-dashboard.component.css'
})
export class AdminDashboardComponent {
  private adminState = inject(AdminStateService);

  kpis = this.adminState.platformKpis;
  health = this.adminState.platformHealth;

  refreshMetrics(): void {
    // Live refresh simulation
  }
}
