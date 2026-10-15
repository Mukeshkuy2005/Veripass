import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

export interface UpcomingExpirationItem {
  id: string;
  credentialName: string;
  credentialType: string;
  daysRemaining: number;
  expiryDate: string;
  status: 'ACTION' | 'VALID' | 'EXPIRED';
}

@Component({
  selector: 'app-upcoming-expirations',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './upcoming-expirations.component.html',
  styleUrl: './upcoming-expirations.component.css'
})
export class UpcomingExpirationsComponent {
  @Input() expirations: UpcomingExpirationItem[] = [];
}
