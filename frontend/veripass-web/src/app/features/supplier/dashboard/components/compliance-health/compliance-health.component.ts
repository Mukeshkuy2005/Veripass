import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

export interface ComplianceTrendPoint {
  month: string;
  score: number;
}

@Component({
  selector: 'app-compliance-health',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './compliance-health.component.html',
  styleUrl: './compliance-health.component.css'
})
export class ComplianceHealthComponent {
  @Input() currentScore: number = 88;
  @Input() history: ComplianceTrendPoint[] = [
    { month: 'May', score: 72 },
    { month: 'Jun', score: 78 },
    { month: 'Jul', score: 85 },
    { month: 'Aug', score: 85 },
    { month: 'Sep', score: 88 }
  ];

  getY(score: number): number {
    // Map 60..100 to 140..20
    const clamped = Math.max(60, Math.min(100, score));
    return 140 - ((clamped - 60) / 40) * 120;
  }

  getX(index: number): number {
    if (this.history.length <= 1) return 150;
    const spacing = 240 / (this.history.length - 1);
    return 40 + index * spacing;
  }

  get pathD(): string {
    if (!this.history || this.history.length === 0) return '';
    return this.history
      .map((pt, i) => `${i === 0 ? 'M' : 'L'} ${this.getX(i)} ${this.getY(pt.score)}`)
      .join(' ');
  }
}
