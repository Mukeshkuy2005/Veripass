import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AdminStateService } from '../../../core/services/admin-state.service';

@Component({
  selector: 'app-verification-management',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './verification-management.component.html',
  styleUrl: './verification-management.component.css'
})
export class VerificationManagementComponent {
  private adminState = inject(AdminStateService);
  metrics = this.adminState.operationsMetrics;
}
