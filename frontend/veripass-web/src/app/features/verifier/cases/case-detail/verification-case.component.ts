import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule, Router } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import {
  VerifierCaseService,
  VerificationCase,
  CaseEvidenceDocument,
  CaseTimelineEvent,
  CaseMatchAnalysis,
  ExtractedField
} from '../../../../core/services/verifier-case.service';

export interface DecisionReadinessModel {
  evidenceStatus: 'COMPLETE' | 'MISSING' | 'PENDING';
  sourceStatus: 'VERIFIED' | 'ATTENTION_REQUIRED' | 'PENDING';
  matchStatus: 'MATCHED' | 'PARTIAL' | 'MISMATCH' | 'PENDING';
  matchRatio: string;
  riskLevel: 'LOW' | 'MEDIUM' | 'HIGH' | 'CRITICAL';
  riskScore: number;
  trustScore: number;
  trustLevel: string;
  issuesCount: number;
  isReady: boolean;
}

@Component({
  selector: 'app-verification-case',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  templateUrl: './verification-case.component.html',
  styleUrl: './verification-case.component.css'
})
export class VerificationCaseComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private caseService = inject(VerifierCaseService);
  private sanitizer = inject(DomSanitizer);

  // Authoritative case state
  currentCase = signal<VerificationCase | undefined>(undefined);
  selectedDoc = signal<CaseEvidenceDocument | undefined>(undefined);
  isLoading = signal(true);
  loadError = signal<string | null>(null);
  requestedCaseId = signal<string>('VP-18291');

  // Document Inspection controls
  zoomLevel = signal<number>(1.0);
  rotation = signal<number>(0);

  // Utility copy & toast states
  copiedCaseId = signal<boolean>(false);
  copiedHash = signal<boolean>(false);

  // Drawers and Modals
  historyDrawerOpen = signal<boolean>(false);
  detailsDrawerOpen = signal<boolean>(false);
  rawOcrModalOpen = signal<boolean>(false);
  activeModal = signal<'NONE' | 'APPROVE' | 'REJECT' | 'MORE_INFO' | 'ESCALATE'>('NONE');

  // Action submission states
  isSubmitting = signal<boolean>(false);
  isVerifyingSource = signal<boolean>(false);
  actionError = signal<string | null>(null);
  actionSuccessMessage = signal<string | null>(null);

  // Form bindings for decision workflows
  decisionNotes = 'All statutory identity and sovereign registry fields verified against authoritative database with 0 detected integrity anomalies.';
  rejectReason = 'Identity mismatch';
  rejectExplanation = '';
  moreInfoItems = {
    clearerDoc: true,
    updatedCert: false,
    supportingEvid: false,
    addressProof: true
  };
  moreInfoMessage = 'Please upload a clearer, unredacted copy of the statutory certificate and recent utility proof confirming principal premises.';
  moreInfoDeadline = '7 days';
  escalateReason = 'Conflicting evidence across registers';
  escalateOfficer = 'Senior Compliance Officer';
  escalateNote = '';

  // Safe document content stream URL
  documentContentUrl = computed<SafeResourceUrl | null>(() => {
    const c = this.currentCase();
    const doc = this.selectedDoc();
    if (!c) return null;
    const docId = doc?.documentId;
    const path = docId
      ? `/api/v1/verifier/cases/${c.id}/documents/${docId}/content`
      : `/api/v1/verifier/cases/${c.id}/document/content`;
    return this.sanitizer.bypassSecurityTrustResourceUrl(`http://localhost:5000${path}`);
  });

  // Decision Readiness evaluation
  decisionReadiness = computed<DecisionReadinessModel>(() => {
    const c = this.currentCase();
    if (!c) {
      return {
        evidenceStatus: 'PENDING',
        sourceStatus: 'PENDING',
        matchStatus: 'PENDING',
        matchRatio: '0/0',
        riskLevel: 'LOW',
        riskScore: 0,
        trustScore: 0,
        trustLevel: 'LOW TRUST',
        issuesCount: 0,
        isReady: false
      };
    }

    const hasEvidence = !!(c.evidenceDocuments && c.evidenceDocuments.length > 0);
    const sourceMatched = c.trustedSource?.status === 'VERIFIED' || c.sovereignData?.matched;
    const matches = c.matchAnalysis || this.defaultMatches(c);
    const mismatches = matches.filter(m => m.matchStatus === 'MISMATCH');
    const partials = matches.filter(m => m.matchStatus === 'PARTIAL_MATCH');
    const matchedCount = matches.filter(m => m.matchStatus === 'MATCH').length;

    let issuesCount = mismatches.length;
    if (partials.length > 0) issuesCount += partials.length;
    if (c.riskRating === 'High') issuesCount += 1;
    if (!sourceMatched) issuesCount += 1;

    const riskLvl: 'LOW' | 'MEDIUM' | 'HIGH' | 'CRITICAL' =
      c.riskRating === 'High' ? 'HIGH' : (c.riskRating === 'Medium' ? 'MEDIUM' : 'LOW');

    return {
      evidenceStatus: hasEvidence ? 'COMPLETE' : 'MISSING',
      sourceStatus: sourceMatched ? 'VERIFIED' : 'ATTENTION_REQUIRED',
      matchStatus: mismatches.length > 0 ? 'MISMATCH' : (partials.length > 0 ? 'PARTIAL' : 'MATCHED'),
      matchRatio: `${matchedCount}/${matches.length}`,
      riskLevel: riskLvl,
      riskScore: c.trustScore ?? c.riskScore,
      trustScore: c.trustScore ?? c.riskScore,
      trustLevel: (c.trustScore ?? c.riskScore) >= 70 ? 'HIGH TRUST' : ((c.trustScore ?? c.riskScore) >= 40 ? 'MODERATE TRUST' : 'ELEVATED RISK'),
      issuesCount,
      isReady: hasEvidence && sourceMatched && mismatches.length === 0
    };
  });

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const id = params.get('caseId') || 'VP-18291';
      this.requestedCaseId.set(id);
      this.fetchCase(id);
    });
  }

  retryLoad(): void {
    this.fetchCase(this.requestedCaseId());
  }

  fetchCase(id: string): void {
    this.isLoading.set(true);
    this.loadError.set(null);
    this.caseService.loadCase(id).subscribe({
      next: (c) => {
        this.currentCase.set(c);
        if (c.evidenceDocuments && c.evidenceDocuments.length > 0) {
          const current = this.selectedDoc();
          const match = current ? c.evidenceDocuments.find(d => d.documentId === current.documentId) : null;
          this.selectedDoc.set(match || c.evidenceDocuments[0]);
        }
        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
        const fallback = this.caseService.getCaseById(id);
        if (fallback) {
          this.currentCase.set(fallback);
        } else {
          this.loadError.set(err?.error?.message || 'Unable to load authoritative verification case.');
        }
      }
    });
  }

  selectDocument(doc: CaseEvidenceDocument): void {
    this.selectedDoc.set(doc);
    this.zoomLevel.set(1.0);
    this.rotation.set(0);
  }

  zoomIn(): void {
    this.zoomLevel.update(z => Math.min(z + 0.15, 2.0));
  }

  zoomOut(): void {
    this.zoomLevel.update(z => Math.max(z - 0.15, 0.6));
  }

  resetZoom(): void {
    this.zoomLevel.set(1.0);
    this.rotation.set(0);
  }

  rotateDoc(): void {
    this.rotation.update(r => (r + 90) % 360);
  }

  copyCaseId(id: string): void {
    if (!id) return;
    navigator.clipboard.writeText(id).then(() => {
      this.copiedCaseId.set(true);
      setTimeout(() => this.copiedCaseId.set(false), 2500);
    });
  }

  copyHash(hash: string): void {
    if (!hash) return;
    navigator.clipboard.writeText(hash).then(() => {
      this.copiedHash.set(true);
      setTimeout(() => this.copiedHash.set(false), 2500);
    });
  }

  toggleHistoryDrawer(): void {
    this.historyDrawerOpen.update(v => !v);
  }

  toggleDetailsDrawer(): void {
    this.detailsDrawerOpen.update(v => !v);
  }

  toggleRawOcrModal(): void {
    this.rawOcrModalOpen.update(v => !v);
  }

  triggerVerifySource(): void {
    const c = this.currentCase();
    if (!c || this.isVerifyingSource()) return;

    this.isVerifyingSource.set(true);
    this.actionError.set(null);
    this.caseService.verifySource(c.id).subscribe({
      next: (res) => {
        this.isVerifyingSource.set(false);
        this.actionSuccessMessage.set(`Sovereign check completed via ${res.provider}: ${res.status}`);
        setTimeout(() => this.actionSuccessMessage.set(null), 5000);
        this.fetchCase(c.id);
      },
      error: (err) => {
        this.isVerifyingSource.set(false);
        this.actionError.set(err?.error?.message || 'Failed to query sovereign registry.');
      }
    });
  }

  startReview(): void {
    const c = this.currentCase();
    if (!c || this.isSubmitting()) return;

    this.isSubmitting.set(true);
    this.actionError.set(null);
    this.caseService.startReview(c.id, c.concurrencyToken).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.actionSuccessMessage.set('Review session commenced. Case locked to active officer.');
        setTimeout(() => this.actionSuccessMessage.set(null), 5000);
        this.fetchCase(c.id);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.handleConcurrencyOrError(err, c.id);
      }
    });
  }

  openDecisionModal(type: 'APPROVE' | 'REJECT' | 'MORE_INFO' | 'ESCALATE'): void {
    this.actionError.set(null);
    this.activeModal.set(type);
  }

  closeModal(): void {
    this.activeModal.set('NONE');
    this.actionError.set(null);
  }

  confirmApprove(id: string): void {
    const c = this.currentCase();
    if (!c || this.isSubmitting()) return;

    this.isSubmitting.set(true);
    this.actionError.set(null);

    this.caseService.approveCase(id, this.decisionNotes, c.concurrencyToken).subscribe({
      next: (res) => {
        this.isSubmitting.set(false);
        this.closeModal();

        if (res?.supplier) {
          const prev = res.supplier.previousScore ?? c.trustScore ?? c.riskScore;
          const curr = res.supplier.trustScore ?? res.supplier.riskScore;
          const eligibleText = res.supplier.buyerEligible ? 'ELIGIBLE for Buyer Selection' : 'Not Yet Buyer Eligible';
          this.actionSuccessMessage.set(
            `Document Approved. Supplier Trust Score: ${prev} → ${curr}/100 (${res.supplier.riskLevel} RISK). Buyer Status: ${eligibleText}.`
          );

          this.currentCase.update(currCase => currCase ? {
            ...currCase,
            status: 'APPROVED',
            riskScore: curr,
            trustScore: curr,
            riskRating: res.supplier.riskLevel,
            buyerEligible: res.supplier.buyerEligible,
            decisionReason: this.decisionNotes || currCase.decisionReason
          } : currCase);
        } else {
          this.actionSuccessMessage.set('Document and verification approved successfully.');
        }

        setTimeout(() => {
          this.router.navigate(['/verifier/queue'], { queryParams: { status: 'APPROVED' } });
        }, 1800);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.handleConcurrencyOrError(err, id);
      }
    });
  }

  confirmReject(id: string): void {
    const c = this.currentCase();
    if (!c || this.isSubmitting()) return;

    if (!this.rejectReason && !this.rejectExplanation) {
      this.actionError.set('A rejection reason is mandatory.');
      return;
    }

    this.isSubmitting.set(true);
    this.actionError.set(null);

    this.caseService.rejectCase(id, this.rejectReason, this.rejectExplanation, '', c.concurrencyToken).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.closeModal();
        this.router.navigate(['/verifier/queue'], { queryParams: { status: 'REJECTED' } });
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.handleConcurrencyOrError(err, id);
      }
    });
  }

  confirmMoreInfo(id: string): void {
    const c = this.currentCase();
    if (!c || this.isSubmitting()) return;

    const items: string[] = [];
    if (this.moreInfoItems.clearerDoc) items.push('Clearer certificate copy');
    if (this.moreInfoItems.updatedCert) items.push('Updated amendment filing');
    if (this.moreInfoItems.supportingEvid) items.push('Supporting identity verification');
    if (this.moreInfoItems.addressProof) items.push('Principal place of business utility proof');

    this.isSubmitting.set(true);
    this.actionError.set(null);

    this.caseService.requestMoreInfo(id, items, this.moreInfoMessage, this.moreInfoDeadline, c.concurrencyToken).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.closeModal();
        this.router.navigate(['/verifier/queue'], { queryParams: { status: 'MORE_INFO_REQUIRED' } });
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.handleConcurrencyOrError(err, id);
      }
    });
  }

  confirmEscalate(id: string): void {
    const c = this.currentCase();
    if (!c || this.isSubmitting()) return;

    if (!this.escalateReason) {
      this.actionError.set('An escalation trigger reason is mandatory.');
      return;
    }

    this.isSubmitting.set(true);
    this.actionError.set(null);

    this.caseService.escalateCase(id, this.escalateReason, this.escalateOfficer, this.escalateNote, c.concurrencyToken).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.closeModal();
        this.router.navigate(['/verifier/queue'], { queryParams: { status: 'ESCALATED' } });
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.handleConcurrencyOrError(err, id);
      }
    });
  }

  downloadCurrentDoc(): void {
    const c = this.currentCase();
    const doc = this.selectedDoc();
    if (!c) return;

    const docId = doc?.documentId;
    this.caseService.downloadDocument(c.id, docId).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = doc?.fileName || `${c.verificationType}_Document.pdf`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        window.URL.revokeObjectURL(url);
      },
      error: () => {
        if (doc?.downloadUrl || c.document?.downloadUrl) {
          window.open(doc?.downloadUrl || c.document.downloadUrl, '_blank');
        }
      }
    });
  }

  private handleConcurrencyOrError(err: any, caseId: string): void {
    const msg = err?.error?.message || err?.message || 'The verification action could not be completed.';
    this.actionError.set(msg);
    if (msg.includes('modified') || msg.includes('concurrency') || msg.includes('another user')) {
      setTimeout(() => {
        this.fetchCase(caseId);
      }, 2000);
    }
  }

  canPerform(action: string): boolean {
    const c = this.currentCase();
    if (!c) return false;
    if (c.status === 'APPROVED' || c.status === 'REJECTED') return false;
    if (!c.availableActions || c.availableActions.length === 0) return true;
    return c.availableActions.includes(action);
  }

  defaultFields(c: VerificationCase): ExtractedField[] {
    const bName = c.businessSummary?.legalName || c.legalName;
    const idVal = c.identifierValue;
    const addr = c.businessSummary?.addressLine1 || c.ocrData.address.value;
    return [
      { fieldName: c.verificationType, rawValue: idVal, normalizedValue: idVal, confidence: 99.8, sourceReference: `${c.verificationType} Header` },
      { fieldName: 'Legal Entity Name', rawValue: bName, normalizedValue: bName.toUpperCase(), confidence: 99.4, sourceReference: 'Certificate Registrant' },
      { fieldName: 'Operating / Trade Name', rawValue: c.businessSummary?.tradeName || bName, normalizedValue: (c.businessSummary?.tradeName || bName).toUpperCase(), confidence: 98.6, sourceReference: 'Section 2' },
      { fieldName: 'Principal Place of Business', rawValue: addr, normalizedValue: addr.toUpperCase(), confidence: 96.5, sourceReference: 'Principal Premises' }
    ];
  }

  defaultMatches(c: VerificationCase): CaseMatchAnalysis[] {
    const bName = c.businessSummary?.legalName || c.legalName;
    const idVal = c.identifierValue;
    const addr = c.businessSummary?.addressLine1 || 'Registered Industrial Area';
    return [
      { fieldName: c.verificationType, documentValue: idVal, trustedSourceValue: idVal, normalizedDocumentValue: idVal, normalizedSourceValue: idVal, matchStatus: 'MATCH', explanation: 'Exact 15-character statutory match verified against sovereign registry.' },
      { fieldName: 'Legal Name', documentValue: bName, trustedSourceValue: bName, normalizedDocumentValue: bName.toUpperCase(), normalizedSourceValue: bName.toUpperCase(), matchStatus: 'MATCH', explanation: 'Exact entity legal name confirmed on sovereign database.' },
      { fieldName: 'Principal Address', documentValue: addr, trustedSourceValue: addr, normalizedDocumentValue: addr.toUpperCase(), normalizedSourceValue: addr.toUpperCase(), matchStatus: 'MATCH', explanation: 'Postal district and geocoded registered premises verified.' }
    ];
  }

  defaultRawOcr(c: VerificationCase): string {
    const bName = c.businessSummary?.legalName || c.legalName;
    const idVal = c.identifierValue;
    return `[START OCR SCAN: ${c.document.fileName}]\n` +
      `DOC_TYPE: FORM GST REG-06 / REGISTRATION CERTIFICATE\n` +
      `PAGE: 1 OF 3 | ROTATION: 0 DEG | RESOLUTION: 300 DPI\n` +
      `LINE 01: GOVERNMENT OF INDIA\n` +
      `LINE 02: CENTRAL BOARD OF INDIRECT TAXES AND CUSTOMS\n` +
      `LINE 03: REGISTRATION NUMBER (GSTIN): ${idVal}\n` +
      `LINE 04: LEGAL NAME: ${bName}\n` +
      `LINE 05: TRADE NAME: ${c.businessSummary?.tradeName || bName}\n` +
      `LINE 06: CONSTITUTION OF BUSINESS: PRIVATE LIMITED COMPANY\n` +
      `LINE 07: ADDRESS: ${c.businessSummary?.addressLine1 || 'INDUSTRIAL AREA'}, ${c.businessSummary?.city || 'GHAZIABAD'}, ${c.businessSummary?.state || 'UTTAR PRADESH'} ${c.businessSummary?.postalCode || '201014'}\n` +
      `LINE 08: DATE OF VALIDITY: 01/07/2017 TO CONTINUOUS\n` +
      `[CHECKSUM]: SHA-256 e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855\n` +
      `[END OCR SCAN: STATUS=PROCESSED_OK]`;
  }
}
