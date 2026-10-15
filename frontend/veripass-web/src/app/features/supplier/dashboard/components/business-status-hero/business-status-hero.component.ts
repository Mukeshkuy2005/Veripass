import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-business-status-hero',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './business-status-hero.component.html',
  styleUrl: './business-status-hero.component.css'
})
export class BusinessStatusHeroComponent {
  @Input() businessName: string = 'My Business';
  @Input() gstin: string = '';
  @Input() cin: string = '';
  @Input() verificationStatus: string = 'UNVERIFIED';
  @Input() verificationScore: number = 10;
  @Input() lastVerifiedDate: string = 'Pending';

  get healthTier(): string {
    if (this.verificationStatus !== 'VERIFIED' && this.verificationScore < 50) return 'Action Required';
    if (this.verificationScore >= 90) return 'Excellent';
    if (this.verificationScore >= 75) return 'Good';
    if (this.verificationScore >= 60) return 'Moderate';
    return 'Action Needed';
  }
}
