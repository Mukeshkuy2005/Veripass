import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule, Router } from '@angular/router';
import {
  VerifierCaseService,
  VerificationCase,
  CaseRiskReviewData
} from '../../../core/services/verifier-case.service';

export interface RiskCaseSummary {
  id: string;
  businessName: string;
  riskRating: 'High' | 'Medium' | 'Low';
  riskScore: number;
}

@Component({
  selector: 'vp-risk-review',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './risk-review.component.html',
  styleUrl: './risk-review.component.css'
})
export class RiskReviewComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private caseService = inject(VerifierCaseService);

  routeCaseId = signal<string | null>(null);
  selectedCaseId = signal<string>('VP-18291');
  isLoading = signal<boolean>(false);
  copiedEvidence = signal<boolean>(false);

  // Authoritative risk data model
  riskData = signal<CaseRiskReviewData>({
    caseId: 'VP-18291',
    businessName: 'ABC INDUSTRIAL DYNAMICS',
    verificationType: 'GST Registration',
    riskScore: 72,
    riskRating: 'HIGH',
    pillars: [
      { name: 'Identity', score: 82, note: 'Business name differs from trusted source' },
      { name: 'Document', score: 74, note: 'PDF structure contains unusual characteristics' },
      { name: 'Source', score: 91, note: 'Statutory registry gateway response reconciled' },
      { name: 'Consistency', score: 61, note: 'Cross-case document similarity detected' }
    ],
    signals: [
      { type: 'IDENTITY_MISMATCH', severity: 'WARNING', title: 'Identity mismatch', description: 'Business name differs from trusted source.' },
      { type: 'DOCUMENT_ALTERATION', severity: 'WARNING', title: 'Possible document alteration', description: 'PDF structure contains unusual characteristics.' },
      { type: 'EXPIRED_CERTIFICATE', severity: 'CRITICAL', title: 'Expired certificate', description: 'Certificate expired 14 days ago.' },
      { type: 'DUPLICATE_DOCUMENT', severity: 'WARNING', title: 'Duplicate document', description: 'Similar document found in another case.' }
    ],
    analysis: {
      summary: 'The system detected a 14-character difference between the submitted business address and the trusted-source address.',
      confidence: 87,
      evidence: {
        submitted: 'Industrial Area, Ghaziabad',
        source: 'Industrial Area, Sahibabad'
      },
      recommendedAction: 'Review manually.'
    },
    decisionBoundary: {
      title: 'AI ASSISTANCE',
      notice: 'AI has identified potential anomalies.',
      disclaimer: 'It does NOT approve or reject this case.',
      finalAuthority: 'Final decision: VERIFIER'
    }
  });

  activeCaseId = computed(() => this.routeCaseId() || this.selectedCaseId());

  highRiskCases = computed<RiskCaseSummary[]>(() => {
    const list: RiskCaseSummary[] = this.caseService.cases()
      .filter(c => c.riskRating === 'High' || c.riskRating === 'Medium' || (c.anomalies && c.anomalies.length > 0))
      .map(c => ({
        id: c.id,
        businessName: c.businessName,
        riskRating: c.riskRating,
        riskScore: c.riskScore
      }));

    if (list.length > 0) return list;
    return [
      {
        id: 'VP-18291',
        businessName: 'ABC INDUSTRIAL DYNAMICS',
        riskRating: 'High',
        riskScore: 72
      },
      {
        id: 'VP-44912',
        businessName: 'ZENITH LOGISTICS LTD',
        riskRating: 'High',
        riskScore: 68
      },
      {
        id: 'VP-99214',
        businessName: 'BHARAT POLYMERS INC',
        riskRating: 'Medium',
        riskScore: 54
      }
    ];
  });

  activeCase = computed(() => {
    const id = this.activeCaseId();
    return this.caseService.getCaseById(id) || this.caseService.cases()[0];
  });

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const id = params.get('caseId');
      if (id) {
        this.routeCaseId.set(id);
        this.caseService.setActiveCaseId(id);
        this.fetchCaseRisk(id);
      } else {
        this.routeCaseId.set(null);
        this.fetchCaseRisk(this.selectedCaseId());
      }
    });
  }

  fetchCaseRisk(caseId: string): void {
    this.isLoading.set(true);
    this.caseService.getCaseRisk(caseId).subscribe({
      next: data => {
        if (data) {
          this.riskData.set(data);
        }
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      }
    });
  }

  setActiveCase(id: string): void {
    this.selectedCaseId.set(id);
    this.caseService.setActiveCaseId(id);
    this.fetchCaseRisk(id);
  }

  copyEvidenceComparison(): void {
    const d = this.riskData();
    const text = `Submitted (OCR): ${d.analysis.evidence.submitted}\nTrusted Source: ${d.analysis.evidence.source}`;
    navigator.clipboard.writeText(text);
    this.copiedEvidence.set(true);
    setTimeout(() => this.copiedEvidence.set(false), 2000);
  }
}
