import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

export interface VerificationCheckItem {
  id: string;
  name: string;
  category: string;
  status: 'VERIFIED' | 'IN_REVIEW' | 'ACTION_REQUIRED' | 'PENDING';
  lastChecked?: string;
}

@Component({
  selector: 'app-verification-overview',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './verification-overview.component.html',
  styleUrl: './verification-overview.component.css'
})
export class VerificationOverviewComponent {
  @Input() checks: VerificationCheckItem[] = [
    { id: 'c1', name: 'Identity (CIN & MCA)', category: 'Corporate', status: 'VERIFIED', lastChecked: '04 Sep 2026' },
    { id: 'c2', name: 'GSTIN Registration', category: 'Taxation', status: 'VERIFIED', lastChecked: '04 Sep 2026' },
    { id: 'c3', name: 'PAN Corporate Entity', category: 'Taxation', status: 'VERIFIED', lastChecked: '04 Sep 2026' },
    { id: 'c4', name: 'Business Address (REG-06)', category: 'Location', status: 'VERIFIED', lastChecked: '04 Sep 2026' },
    { id: 'c5', name: 'Bank Account Penny-Drop', category: 'Banking', status: 'VERIFIED', lastChecked: '19 Aug 2026' },
    { id: 'c6', name: 'Certifications (ISO 9001)', category: 'Quality', status: 'ACTION_REQUIRED', lastChecked: 'Expiring soon' }
  ];

  get completedCount(): number {
    return this.checks.filter(c => c.status === 'VERIFIED').length;
  }
}
