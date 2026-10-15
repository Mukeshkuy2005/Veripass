import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { VerifierRiskDistribution } from '../../models/verifier-dashboard.model';

@Component({
  selector: 'app-risk-distribution',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './risk-distribution.component.html',
  styleUrl: './risk-distribution.component.css'
})
export class RiskDistributionComponent {
  risk = input<VerifierRiskDistribution | null>(null);
  loading = input<boolean>(false);

  hasAnyCases(r: VerifierRiskDistribution | null): boolean {
    if (!r) return false;
    return (r.low + r.medium + r.high) > 0;
  }
}
