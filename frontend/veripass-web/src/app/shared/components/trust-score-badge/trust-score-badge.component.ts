import { Component, Input, computed } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-trust-score-badge',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './trust-score-badge.component.html',
  styleUrl: './trust-score-badge.component.css'
})
export class TrustScoreBadgeComponent {
  @Input() score = 0;
  @Input() showLabel = true;
  @Input() size = 80;

  private circumference = 2 * Math.PI * 42; // ~263.89

  strokeDasharray = computed(() => `${this.circumference} ${this.circumference}`);
  strokeDashoffset = computed(() => {
    const clampedScore = Math.min(Math.max(this.score, 0), 100);
    const progress = clampedScore / 100;
    return this.circumference * (1 - progress);
  });

  scoreClass = computed(() => {
    if (this.score >= 85) return 'tier-excellent';
    if (this.score >= 70) return 'tier-good';
    if (this.score >= 50) return 'tier-moderate';
    return 'tier-risk';
  });

  tierLabel = computed(() => {
    if (this.score >= 85) return 'Verified Sovereign Tier';
    if (this.score >= 70) return 'Compliant Partner';
    if (this.score >= 50) return 'Pending Validation';
    return 'High Compliance Risk';
  });
}
