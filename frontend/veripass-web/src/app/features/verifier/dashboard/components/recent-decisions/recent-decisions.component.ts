import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { VerifierRecentDecision } from '../../models/verifier-dashboard.model';

@Component({
  selector: 'app-recent-decisions',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './recent-decisions.component.html',
  styleUrl: './recent-decisions.component.css'
})
export class RecentDecisionsComponent {
  decisions = input<VerifierRecentDecision[]>([]);
  loading = input<boolean>(false);

  getDecisionBadge(decision: string): { icon: string; text: string; cssClass: string } {
    const d = (decision || '').toUpperCase();
    if (d === 'APPROVED' || d === 'VERIFIED') {
      return { icon: '✓', text: 'Approved', cssClass: 'badge-approved' };
    }
    if (d === 'REJECTED') {
      return { icon: '✕', text: 'Rejected', cssClass: 'badge-rejected' };
    }
    if (d === 'ESCALATED') {
      return { icon: '↗', text: 'Escalated', cssClass: 'badge-escalated' };
    }
    return { icon: '•', text: decision, cssClass: 'badge-default' };
  }
}
