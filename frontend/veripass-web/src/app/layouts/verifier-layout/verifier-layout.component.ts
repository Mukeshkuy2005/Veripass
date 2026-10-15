import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { VerifierCaseService } from '../../core/services/verifier-case.service';

@Component({
  selector: 'app-verifier-layout',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './verifier-layout.component.html',
  styleUrl: './verifier-layout.component.css'
})
export class VerifierLayoutComponent {
  private router = inject(Router);
  private auth = inject(AuthService);
  private caseService = inject(VerifierCaseService);

  queueCount = this.caseService.queueCount;
  activeCaseId = this.caseService.activeCaseId;
  riskCount = this.caseService.riskCount;
  escalatedCount = this.caseService.escalatedCount;

  onQuickSearch(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.value) {
      this.router.navigate(['/verifier/queue'], { queryParams: { q: input.value } });
    }
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/auth/login']);
  }
}
