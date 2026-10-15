import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule, Router } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import {
  VerifierCaseService,
  DocumentReviewDetail,
  DocumentReviewItem,
  DocumentVersionSummary,
  OcrReview,
  OcrFieldReview,
  DocumentValidationSummary,
  DocumentValidationItem,
  DocumentIntegrityReview,
  CaseEvidenceDocument
} from '../../../../core/services/verifier-case.service';

@Component({
  selector: 'app-document-review',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './document-review.component.html',
  styleUrl: './document-review.component.css'
})
export class DocumentReviewComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private caseService = inject(VerifierCaseService);
  private sanitizer = inject(DomSanitizer);

  // Core state
  caseId = signal<string>('');
  state = signal<'LOADING' | 'READY' | 'ERROR'>('LOADING');
  errorMessage = signal<string | null>(null);
  review = signal<DocumentReviewDetail | null>(null);

  // Document Inspection canvas state
  zoomLevel = signal<number>(1.0);
  rotation = signal<number>(0);
  selectedField = signal<OcrFieldReview | null>(null);
  showRawOcrModal = signal<boolean>(false);
  copiedHash = signal<boolean>(false);
  copiedRawText = signal<boolean>(false);
  toastMessage = signal<string | null>(null);

  // Subsystem processing states
  isReprocessingOcr = signal<boolean>(false);
  isVerifyingIntegrity = signal<boolean>(false);
  isDownloading = signal<boolean>(false);

  // Safe document content stream URL
  documentContentUrl = computed<SafeResourceUrl | null>(() => {
    const rev = this.review();
    if (!rev?.document?.contentUrl) return null;
    return this.sanitizer.bypassSecurityTrustResourceUrl(`http://localhost:5000${rev.document.contentUrl}`);
  });

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const id = params.get('caseId');
      if (id) {
        this.caseId.set(id);
        this.caseService.setActiveCaseId(id);
        this.route.queryParamMap.subscribe(queryParams => {
          const docId = queryParams.get('documentId') || undefined;
          const versionStr = queryParams.get('version');
          const versionNum = versionStr ? parseInt(versionStr, 10) : undefined;
          this.loadDocumentReview(id, docId, versionNum);
        });
      } else {
        this.state.set('ERROR');
        this.errorMessage.set('Case ID parameter missing from route.');
      }
    });
  }

  loadDocumentReview(caseId: string, documentId?: string, version?: number): void {
    this.state.set('LOADING');
    this.errorMessage.set(null);
    this.selectedField.set(null);

    this.caseService.loadDocumentReview(caseId, documentId, version).subscribe({
      next: (detail: DocumentReviewDetail) => {
        this.review.set(detail);
        this.state.set('READY');
      },
      error: (err: any) => {
        console.error('Failed to load document review:', err);
        this.state.set('ERROR');
        if (err?.status === 403) {
          this.errorMessage.set('Access Denied: You do not have compliance officer permissions or this document is outside case scope.');
        } else if (err?.status === 404) {
          this.errorMessage.set(`Document or Case '${caseId}' was not found in authoritative records.`);
        } else {
          this.errorMessage.set(err?.error?.message || 'Unable to connect to verification document pipeline.');
        }
      }
    });
  }

  // Document switching
  selectDocument(doc: CaseEvidenceDocument): void {
    const cid = this.caseId();
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { documentId: doc.documentId },
      queryParamsHandling: 'merge'
    });
  }

  // Version switching
  selectVersion(versionSummary: DocumentVersionSummary): void {
    const rev = this.review();
    if (!rev) return;
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { documentId: rev.document.id, version: versionSummary.versionNumber },
      queryParamsHandling: 'merge'
    });
  }

  // Bounding box highlighting
  highlightField(field: OcrFieldReview): void {
    if (this.selectedField()?.fieldName === field.fieldName) {
      this.selectedField.set(null);
    } else {
      this.selectedField.set(field);
    }
  }

  clearHighlight(): void {
    this.selectedField.set(null);
  }

  // Canvas zoom/rotate controls
  zoomIn(): void {
    this.zoomLevel.update(z => Math.min(2.5, Math.round((z + 0.15) * 100) / 100));
  }

  zoomOut(): void {
    this.zoomLevel.update(z => Math.max(0.5, Math.round((z - 0.15) * 100) / 100));
  }

  resetZoom(): void {
    this.zoomLevel.set(1.0);
    this.rotation.set(0);
  }

  rotateDoc(): void {
    this.rotation.update(r => (r + 90) % 360);
  }

  // Hash copying
  copyHash(hash: string): void {
    if (!hash) return;
    navigator.clipboard.writeText(hash).then(() => {
      this.copiedHash.set(true);
      this.showToast('SHA-256 hash copied to clipboard');
      setTimeout(() => this.copiedHash.set(false), 2500);
    });
  }

  // File Download
  downloadDocument(): void {
    const rev = this.review();
    if (!rev) return;

    this.isDownloading.set(true);
    this.showToast(`Initiating secure authorized download of ${rev.document.fileName}...`);

    this.caseService.downloadDocument(rev.caseId, rev.document.id).subscribe({
      next: (blob: Blob) => {
        this.isDownloading.set(false);
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = rev.document.fileName || 'evidence_document.pdf';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        window.URL.revokeObjectURL(url);
        this.showToast(`Document downloaded successfully`);
      },
      error: (err: any) => {
        this.isDownloading.set(false);
        console.error('Download failed:', err);
        this.showToast('Download failed: ' + (err?.error?.message || 'Access denied or server error'));
      }
    });
  }

  // Reprocess OCR
  reprocessOcr(): void {
    const rev = this.review();
    if (!rev) return;

    this.isReprocessingOcr.set(true);
    this.showToast('Re-running OCR pipeline with VeriPass Vision OCR Engine...');

    this.caseService.reprocessDocumentOcr(rev.caseId, rev.document.id).subscribe({
      next: (newOcr: OcrReview) => {
        this.isReprocessingOcr.set(false);
        this.review.update(cur => cur ? { ...cur, ocr: newOcr } : cur);
        this.showToast('OCR extraction completed and persisted successfully');
      },
      error: (err: any) => {
        this.isReprocessingOcr.set(false);
        console.error('OCR reprocess failed:', err);
        this.showToast('OCR processing failed: ' + (err?.error?.message || 'Server error'));
      }
    });
  }

  // Re-verify Integrity
  reverifyIntegrity(): void {
    const rev = this.review();
    if (!rev) return;

    this.isVerifyingIntegrity.set(true);
    this.showToast('Recalculating file hash digest and inspecting metadata...');

    this.caseService.validateDocumentIntegrity(rev.caseId, rev.document.id).subscribe({
      next: (newIntegrity: DocumentIntegrityReview) => {
        this.isVerifyingIntegrity.set(false);
        this.review.update(cur => cur ? { ...cur, integrity: newIntegrity } : cur);
        this.showToast('Document cryptographic integrity verified');
      },
      error: (err: any) => {
        this.isVerifyingIntegrity.set(false);
        console.error('Integrity check failed:', err);
        this.showToast('Integrity verification failed: ' + (err?.error?.message || 'Server error'));
      }
    });
  }

  // Raw OCR Modal
  openRawOcr(): void {
    this.showRawOcrModal.set(true);
  }

  closeRawOcr(): void {
    this.showRawOcrModal.set(false);
  }

  copyRawOcrText(): void {
    const text = this.review()?.ocr?.rawOcrText;
    if (!text) return;
    navigator.clipboard.writeText(text).then(() => {
      this.copiedRawText.set(true);
      setTimeout(() => this.copiedRawText.set(false), 2000);
    });
  }

  getFieldVal(index: number, fallback: string = ''): string {
    const f = this.review()?.ocr?.fields;
    return f && f[index] ? f[index].rawValue : fallback;
  }

  // Navigation: Close review returns directly to V3 Verification Case
  closeReview(): void {
    const cid = this.caseId() || this.review()?.caseId;
    this.router.navigate(['/verifier/cases', cid]);
  }

  private showToast(msg: string): void {
    this.toastMessage.set(msg);
    setTimeout(() => {
      if (this.toastMessage() === msg) {
        this.toastMessage.set(null);
      }
    }, 4000);
  }
}
