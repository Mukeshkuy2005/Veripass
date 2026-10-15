import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { VerifierPerformance } from '../../models/verifier-dashboard.model';

@Component({
  selector: 'app-verification-performance',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './verification-performance.component.html',
  styleUrl: './verification-performance.component.css'
})
export class VerificationPerformanceComponent {
  performance = input<VerifierPerformance | null>(null);
  loading = input<boolean>(false);

  formatDuration(seconds: number): string {
    if (!seconds || seconds <= 0) return '0s';
    const mins = Math.floor(seconds / 60);
    const secs = Math.floor(seconds % 60);
    if (mins > 0) {
      return `${mins}m ${secs}s`;
    }
    return `${secs}s`;
  }
}
