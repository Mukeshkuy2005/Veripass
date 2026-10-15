import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { VerifierPriorityCase } from '../../models/verifier-dashboard.model';

@Component({
  selector: 'app-priority-cases',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './priority-cases.component.html',
  styleUrl: './priority-cases.component.css'
})
export class PriorityCasesComponent {
  cases = input<VerifierPriorityCase[]>([]);
  loading = input<boolean>(false);

  formatRelativeTime(submittedAt: string): string {
    if (!submittedAt) return 'Recently';
    const date = new Date(submittedAt);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMin = Math.max(1, Math.floor(diffMs / 60000));

    if (diffMin < 60) {
      return `Submitted ${diffMin} min ago`;
    }
    const diffHours = Math.floor(diffMin / 60);
    if (diffHours < 24) {
      return `Submitted ${diffHours}h ago`;
    }
    const diffDays = Math.floor(diffHours / 24);
    return `Submitted ${diffDays}d ago`;
  }

  getPriorityClass(priority: string): string {
    const p = (priority || '').toUpperCase();
    if (p === 'HIGH' || p === 'URGENT') return 'priority-high';
    if (p === 'MEDIUM') return 'priority-med';
    return 'priority-low';
  }

  getPriorityBadge(priority: string): { icon: string; text: string } {
    const p = (priority || '').toUpperCase();
    if (p === 'HIGH' || p === 'URGENT') {
      return { icon: '🔴', text: 'HIGH' };
    }
    if (p === 'MEDIUM') {
      return { icon: '🟠', text: 'MEDIUM' };
    }
    return { icon: '🟡', text: 'LOW' };
  }
}
