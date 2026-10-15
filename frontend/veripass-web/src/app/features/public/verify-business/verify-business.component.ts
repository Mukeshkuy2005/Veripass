import { Component, signal, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';

interface PublicLookupResult {
  status: 'VERIFIED' | 'INVALID';
  businessName: string;
  passportId: string;
  verificationScore: number;
  lastVerifiedDate: string;
}

@Component({
  selector: 'app-verify-business',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './verify-business.component.html',
  styleUrl: './verify-business.component.css'
})
export class VerifyBusinessComponent implements OnInit {
  private route = inject(ActivatedRoute);

  searchQuery = '';
  currentState = signal<'IDLE' | 'LOADING' | 'VERIFIED' | 'INVALID' | 'SCANNING'>('IDLE');
  result = signal<PublicLookupResult | null>(null);

  ngOnInit(): void {
    // Check if query param passed (e.g. /verify?id=VP-ABCD123)
    this.route.queryParams.subscribe(params => {
      if (params['id'] || params['passportId']) {
        this.searchQuery = params['id'] || params['passportId'];
        this.executeLookup();
      }
    });
  }

  executeLookup(): void {
    const query = this.searchQuery.trim().toUpperCase();
    if (!query) return;

    this.currentState.set('LOADING');

    // Simulate verified lookups for valid demo passports
    setTimeout(() => {
      const isABC = query === 'VP-IN-2026-92644' || query === '27AABCU9603R1ZM' || query.includes('ABC') || query === 'VP-ABCD123';
      const isAxionis = query === 'VP-IN-2026-46190' || query === '29AABCA1234F1Z5' || query.includes('AXIONIS') || query === 'VP-XZY8921';

      if (isABC || isAxionis) {
        this.result.set({
          status: 'VERIFIED',
          businessName: isAxionis ? 'Axionis manufacturing pvt ltd' : 'ABC INDUSTRIAL DYNAMICS',
          passportId: isAxionis ? 'VP-IN-2026-46190' : 'VP-IN-2026-92644',
          verificationScore: isAxionis ? 82 : 88,
          lastVerifiedDate: '07 Oct 2026'
        });
        this.currentState.set('VERIFIED');
      } else {
        this.result.set(null);
        this.currentState.set('INVALID');
      }
    }, 600);
  }

  simulateQrScan(): void {
    this.searchQuery = 'VP-IN-2026-92644';
    this.executeLookup();
  }

  resetLookup(): void {
    this.currentState.set('IDLE');
    this.searchQuery = '';
    this.result.set(null);
  }
}
