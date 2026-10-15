import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AdminStateService } from '../../../core/services/admin-state.service';

@Component({
  selector: 'app-admin-risk-management',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './admin-risk-management.component.html',
  styleUrl: './admin-risk-management.component.css'
})
export class AdminRiskManagementComponent {
  private adminState = inject(AdminStateService);
  risk = this.adminState.riskDistribution;
  businesses = this.adminState.businesses;
}
