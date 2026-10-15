import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-core-metrics-grid',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './core-metrics-grid.component.html',
  styleUrl: './core-metrics-grid.component.css'
})
export class CoreMetricsGridComponent {
  @Input() verificationScore: number = 94;
  @Input() verificationLabel: string = 'Verified';
  @Input() verificationDetail: string = '7 of 8 checks complete';

  @Input() complianceScore: number = 88;
  @Input() complianceLabel: string = 'Good';
  @Input() complianceDetail: string = '1 active alert';

  @Input() totalCredentials: number = 8;
  @Input() verifiedCredentials: number = 7;

  @Input() buyerRequestsCount: number = 3;
  @Input() pendingRequestsCount: number = 1;
}
