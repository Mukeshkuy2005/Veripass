import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { VerifierDashboardSummary } from '../../models/verifier-dashboard.model';

@Component({
  selector: 'app-verification-kpi-cards',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './verification-kpi-cards.component.html',
  styleUrl: './verification-kpi-cards.component.css'
})
export class VerificationKpiCardsComponent {
  summary = input<VerifierDashboardSummary | null>(null);
  loading = input<boolean>(false);
}
