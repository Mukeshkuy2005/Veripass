import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BusinessPassportDto } from '../../../core/models/models';
import { TrustScoreBadgeComponent } from '../trust-score-badge/trust-score-badge.component';
import { StatusBadgeComponent } from '../status-badge/status-badge.component';

@Component({
  selector: 'app-passport-card',
  standalone: true,
  imports: [CommonModule, TrustScoreBadgeComponent, StatusBadgeComponent],
  templateUrl: './passport-card.component.html',
  styleUrl: './passport-card.component.css'
})
export class PassportCardComponent {
  @Input() passport: BusinessPassportDto | null = null;
}
