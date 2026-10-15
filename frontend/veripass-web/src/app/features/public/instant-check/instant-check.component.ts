import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { TrustScoreBadgeComponent } from '../../../shared/components/trust-score-badge/trust-score-badge.component';

@Component({
  selector: 'app-instant-check',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, TrustScoreBadgeComponent],
  templateUrl: './instant-check.component.html',
  styleUrl: './instant-check.component.css'
})
export class InstantCheckComponent {
  searchQuery = '27AABCU9603R1ZM';
  searching = signal(false);

  result = signal<any>({
    name: 'ABC INDUSTRIAL DYNAMICS',
    gstin: '27AABCU9603R1ZM',
    pan: 'AABCU9603R',
    address: 'MIDC Industrial Area, Andheri East, Mumbai, Maharashtra - 400093',
    score: 88,
    passportId: 'VP-IN-2026-92644'
  });

  fillAndSearch(gstin: string): void {
    this.searchQuery = gstin;
    this.executeSearch();
  }

  executeSearch(): void {
    this.searching.set(true);
    setTimeout(() => {
      this.searching.set(false);
      const isAxionis = this.searchQuery.includes('29AABCA') || this.searchQuery.toUpperCase().includes('AXIONIS');
      if (isAxionis) {
        this.result.set({
          name: 'Axionis manufacturing pvt ltd',
          gstin: '29AABCA1234F1Z5',
          pan: 'AABCA1234F',
          address: 'Electronic City Phase 1, Bengaluru, Karnataka - 560100',
          score: 82,
          passportId: 'VP-IN-2026-46190'
        });
      } else {
        this.result.set({
          name: 'ABC INDUSTRIAL DYNAMICS',
          gstin: '27AABCU9603R1ZM',
          pan: 'AABCU9603R',
          address: 'MIDC Industrial Area, Andheri East, Mumbai, Maharashtra - 400093',
          score: 88,
          passportId: 'VP-IN-2026-92644'
        });
      }
    }, 600);
  }
}
