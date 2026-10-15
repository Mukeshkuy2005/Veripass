import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { VerifierCaseService, VerificationCase } from '../../../../core/services/verifier-case.service';

type DecisionMode = 'APPROVE' | 'REJECT' | 'MORE_INFO' | 'ESCALATE';

@Component({
  selector: 'vp-verification-decision',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './verification-decision.component.html',
  styleUrl: './verification-decision.component.css'
})
export class VerificationDecisionComponent {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private caseService = inject(VerifierCaseService);

  caseId = signal<string>('');
  mode = signal<DecisionMode>('APPROVE');
  attemptedSubmit = false;

  // Form bindings
  approveNotes = 'Verified against live sovereign gateway. Extracted OCR matches certified register with 0 detected integrity anomalies.';
  
  selectedRejectionReason = 'Source mismatch';
  rejectionExplanation = '';
  internalNotes = '';
  rejectionReasons = [
    'Identity mismatch',
    'Invalid document',
    'Expired credential',
    'Source mismatch',
    'Suspected alteration',
    'Duplicate document',
    'Insufficient evidence',
    'Other'
  ];

  missingItems = {
    clearerDoc: true,
    updatedCert: false,
    supportingProof: true,
    signatoryProof: false
  };
  supplierMessage = 'Please provide an updated, high-resolution copy of your registration certificate and supporting utility bill showing matching registered address.';
  responseDeadline = '7 days';

  selectedEscalationReason = 'High-risk anomaly';
  targetOfficer = 'Senior Compliance Officer';
  escalationNotes = '';
  escalationReasons = [
    'High-risk anomaly',
    'Conflicting evidence',
    'Suspected fraud',
    'Policy exception',
    'Requires senior authority'
  ];

  activeCase = computed(() => {
    const id = this.caseId();
    return id ? this.caseService.getCaseById(id) : undefined;
  });

  constructor() {
    this.route.paramMap.subscribe(params => {
      const id = params.get('caseId');
      if (id) {
        this.caseId.set(id);
      }
    });

    this.route.queryParamMap.subscribe(qp => {
      const initialMode = qp.get('mode') as DecisionMode;
      if (initialMode && ['APPROVE', 'REJECT', 'MORE_INFO', 'ESCALATE'].includes(initialMode)) {
        this.mode.set(initialMode);
      }
    });
  }

  setMode(m: DecisionMode): void {
    this.mode.set(m);
    this.attemptedSubmit = false;
  }

  submitApproval(id: string): void {
    this.caseService.approveCase(id, this.approveNotes);
    this.router.navigate(['/verifier/cases', id]);
  }

  submitRejection(id: string): void {
    this.attemptedSubmit = true;
    if (!this.rejectionExplanation.trim()) {
      return;
    }
    this.caseService.rejectCase(
      id,
      this.selectedRejectionReason,
      this.rejectionExplanation,
      this.internalNotes
    );
    this.router.navigate(['/verifier/cases', id]);
  }

  submitMoreInfo(id: string): void {
    const items: string[] = [];
    if (this.missingItems.clearerDoc) items.push('Clearer document');
    if (this.missingItems.updatedCert) items.push('Updated certificate');
    if (this.missingItems.supportingProof) items.push('Supporting proof');
    if (this.missingItems.signatoryProof) items.push('Authorized signatory resolution');

    this.caseService.requestMoreInfo(id, items, this.supplierMessage, this.responseDeadline);
    this.router.navigate(['/verifier/cases', id]);
  }

  submitEscalation(id: string): void {
    this.attemptedSubmit = true;
    if (!this.escalationNotes.trim()) {
      this.escalationNotes = `Case escalated due to ${this.selectedEscalationReason}. Requires senior adjudication.`;
    }
    this.caseService.escalateCase(
      id,
      this.selectedEscalationReason,
      this.targetOfficer,
      this.escalationNotes
    );
    this.router.navigate(['/verifier/cases', id]);
  }
}
