import { Component, Input, computed } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './status-badge.component.html',
  styleUrl: './status-badge.component.css'
})
export class StatusBadgeComponent {
  @Input() status: string = 'UNVERIFIED';
  @Input() showDot: boolean = true;

  badgeClass = computed(() => {
    const s = this.status?.toUpperCase() || '';
    if (s === 'VERIFIED' || s === 'APPROVED' || s === 'ACTIVE') return 'badge-verified';
    if (s === 'PENDING' || s === 'IN_PROGRESS' || s === 'SUBMITTED' || s === 'INVITED') return 'badge-pending';
    return 'badge-flagged';
  });

  statusText = computed(() => this.status?.replace('_', ' ') || 'UNVERIFIED');
}
