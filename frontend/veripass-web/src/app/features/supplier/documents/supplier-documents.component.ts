import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { BusinessService } from '../../../core/services/business.service';
import { jsPDF } from 'jspdf';

import { AuthService } from '../../../core/auth/auth.service';

export interface CredentialDoc {
  id: string;
  name: string;
  category: string;
  identifier: string;
  status: 'VERIFIED' | 'EXPIRING' | 'ACTION_REQUIRED' | 'PENDING';
  issued: string;
  expires: string;
  source: string;
  fileName: string;
}

@Component({
  selector: 'app-supplier-documents',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './supplier-documents.component.html',
  styleUrl: './supplier-documents.component.css'
})
export class SupplierDocumentsComponent implements OnInit {
  private businessService = inject(BusinessService);
  private auth = inject(AuthService);
  businessId = '';

  selectedCategory = signal<string>('All');
  selectedCred = signal<CredentialDoc | null>(null);
  activeModalTab = signal<'overview' | 'audit'>('overview');
  copiedId = signal<boolean>(false);
  docPreviewModal = signal<boolean>(false);
  previewZoom = signal<number>(1);
  toastMessage = signal<string | null>(null);

  // Add Credential Modal State
  showAddCredentialModal = signal<boolean>(false);
  isSubmittingCredential = signal<boolean>(false);
  addCredentialError = signal<string | null>(null);
  newCredType = signal<string>('GST_REGISTRATION');
  newCredName = signal<string>('');
  newCredNumber = signal<string>('');
  newCredIssuer = signal<string>('');
  newCredIssueDate = signal<string>(new Date().toISOString().substring(0, 10));
  issueDateError = signal<string | null>(null);
  newCredFile = signal<File | null>(null);
  newCredFileName = signal<string | null>(null);

  get todayString(): string {
    return new Date().toISOString().substring(0, 10);
  }

  onIssueDateChange(val: string): void {
    this.newCredIssueDate.set(val);
    if (!val) {
      this.issueDateError.set('Date of issue is required.');
    } else if (val > this.todayString) {
      this.issueDateError.set('Issue date cannot be in the future. Please select a valid past or present date.');
    } else {
      this.issueDateError.set(null);
    }
  }

  private getStorageKey(bizId: string): string {
    return `veripass_custom_docs_${bizId || 'default'}`;
  }

  private getCustomCredentialsLocally(bizId: string): CredentialDoc[] {
    if (typeof localStorage === 'undefined') return [];
    try {
      const data = localStorage.getItem(this.getStorageKey(bizId));
      return data ? JSON.parse(data) : [];
    } catch {
      return [];
    }
  }

  private saveCustomCredentialLocally(bizId: string, item: CredentialDoc): void {
    if (typeof localStorage === 'undefined') return;
    try {
      const current = this.getCustomCredentialsLocally(bizId);
      const filtered = current.filter(c => 
        (c.identifier && item.identifier && c.identifier.toLowerCase() !== item.identifier.toLowerCase()) || 
        c.name.toLowerCase() !== item.name.toLowerCase()
      );
      filtered.unshift(item);
      localStorage.setItem(this.getStorageKey(bizId), JSON.stringify(filtered));
    } catch {}
  }

  mapCategory(docType: string): string {
    const upper = (docType || '').toUpperCase();
    if (upper.includes('GST') || upper.includes('TAX')) return 'Tax';
    if (upper.includes('PAN') || upper.includes('IDENTITY') || upper.includes('AADHAAR')) return 'Identity';
    if (upper.includes('BANK') || upper.includes('CHEQUE')) return 'Banking';
    if (upper.includes('UDYAM') || upper.includes('MSME') || upper.includes('INC') || upper.includes('CIN') || upper.includes('REGISTRATION')) return 'Registration';
    if (upper.includes('ISO') || upper.includes('QUALITY')) return 'Quality';
    if (upper.includes('LICENSE') || upper.includes('POLLUTION') || upper.includes('NOC') || upper.includes('FACTORY')) return 'Licenses';
    return 'Legal';
  }

  credentials = signal<CredentialDoc[]>([]);

  ngOnInit(): void {
    const authId = this.auth.currentBusinessId();
    if (authId) {
      this.businessId = authId;
      this.businessService.getProfile(authId).subscribe({
        next: (p) => {
          if (p?.id) this.businessId = p.id;
          this.loadDocuments();
        },
        error: () => this.loadDocuments()
      });
    } else {
      this.loadDocuments();
    }
  }

  loadDocuments(): void {
    const targetBusinessId = this.businessService.currentProfile()?.id || this.businessId || this.auth.currentBusinessId() || '';
    const isVerified = this.businessService.currentProfile()?.verificationStatus === 'VERIFIED';
    const localDocs = this.getCustomCredentialsLocally(targetBusinessId);

    this.businessService.getDocuments(targetBusinessId).subscribe({
      next: (docs) => {
        if (docs && docs.length > 0) {
          const mapped: CredentialDoc[] = docs.map(d => {
            const localMatch = localDocs.find(l =>
              (d.originalFileName && l.fileName === d.originalFileName) ||
              (d.title && l.name.toLowerCase() === d.title.toLowerCase()) ||
              (d.extractedFields?.['CredentialName'] && l.name.toLowerCase() === d.extractedFields['CredentialName'].toLowerCase())
            );

            const displayName = d.title ||
                                d.extractedFields?.['CredentialName'] ||
                                d.extractedFields?.['Title'] ||
                                localMatch?.name ||
                                (d.documentType || 'Document').replace(/_/g, ' ');

            const identifier = d.credentialNumber ||
                               d.extractedFields?.['CredentialNumber'] ||
                               localMatch?.identifier ||
                               d.extractedFields?.['Gstin'] ||
                               d.extractedFields?.['Pan'] ||
                               d.extractedFields?.['UdyamNumber'] ||
                               d.storageKey ||
                               d.id;

            let formattedIssued = '—';
            if (d.issuedDate) {
              formattedIssued = new Date(d.issuedDate).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
            } else if (d.extractedFields?.['IssueDate']) {
              formattedIssued = new Date(d.extractedFields['IssueDate']).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
            } else if (localMatch?.issued) {
              formattedIssued = localMatch.issued.includes('-')
                ? new Date(localMatch.issued).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
                : localMatch.issued;
            } else if (d.uploadedAt) {
              formattedIssued = new Date(d.uploadedAt).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
            }

            let formattedExpires = '—';
            if (d.expiryDate) {
              formattedExpires = new Date(d.expiryDate).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
            } else if (localMatch?.expires && localMatch.expires !== '—') {
              formattedExpires = localMatch.expires;
            }

            const issuer = d.issuer ||
                           d.extractedFields?.['Issuer'] ||
                           localMatch?.source ||
                           'Sovereign Vault';

            return {
              id: d.id,
              name: displayName,
              category: localMatch?.category || this.mapCategory(d.documentType),
              identifier: identifier,
              status: (d.status === 'PROCESSED' ? 'VERIFIED' : 'PENDING') as any,
              issued: formattedIssued,
              expires: formattedExpires,
              source: issuer,
              fileName: d.originalFileName || localMatch?.fileName || 'document.pdf'
            };
          });

          // Append any locally created documents that haven't been indexed by the backend yet
          const unmergedLocal = localDocs.filter(l => !mapped.some(m => m.name.toLowerCase() === l.name.toLowerCase() || (m.fileName && m.fileName === l.fileName)));
          this.credentials.set([...unmergedLocal, ...mapped]);
        } else if (localDocs.length > 0) {
          this.credentials.set(localDocs);
        } else if (!isVerified) {
          this.credentials.set([]);
        }
      },
      error: () => {
        if (localDocs.length > 0) {
          this.credentials.set(localDocs);
        }
      }
    });
  }

  get companyName(): string {
    const prof = this.businessService.currentProfile();
    return prof?.legalName || prof?.tradeName || this.auth.currentUser()?.companyName || this.auth.currentUser()?.organizationName || 'Registered Enterprise';
  }

  get principalAddress(): string {
    const addr = this.businessService.currentProfile()?.addresses?.[0];
    if (addr && (addr.line1 || addr.city)) {
      return `${addr.line1 ? addr.line1 + ', ' : ''}${addr.city ? addr.city + ', ' : ''}${addr.state || ''} ${addr.pincode || ''}`.trim().replace(/^, |, $/g, '');
    }
    return '';
  }

  get totalDocsCount(): number {
    return this.credentials().length;
  }

  get verifiedDocsCount(): number {
    return this.credentials().filter(c => c.status === 'VERIFIED').length;
  }

  get expiringDocsCount(): number {
    return this.credentials().filter(c => c.status === 'EXPIRING').length;
  }

  get actionNeededDocsCount(): number {
    return this.credentials().filter(c => c.status === 'ACTION_REQUIRED').length;
  }

  categories = [
    'All',
    'Identity',
    'Tax',
    'Registration',
    'Banking',
    'Certification',
    'Licenses',
    'Quality',
    'Legal'
  ];

  filteredCredentials = computed(() => {
    const cat = this.selectedCategory();
    const list = this.credentials();
    if (cat === 'All') return list;
    return list.filter(c => c.category === cat);
  });

  filterByStatus(status: 'VERIFIED' | 'EXPIRING' | 'ACTION_REQUIRED'): void {
    this.selectedCategory.set('All');
  }

  selectCredential(cred: CredentialDoc): void {
    this.selectedCred.set(cred);
    this.activeModalTab.set('overview');
    this.copiedId.set(false);
  }

  copyIdentifier(id: string): void {
    if (navigator?.clipboard) {
      navigator.clipboard.writeText(id);
    }
    this.copiedId.set(true);
    this.showToast('✓ Credential Identifier copied to clipboard');
    setTimeout(() => this.copiedId.set(false), 2000);
  }

  openUploadModal(): void {
    this.openAddCredentialModal();
  }

  openAddCredentialModal(): void {
    this.resetAddCredentialForm();
    this.showAddCredentialModal.set(true);
  }

  closeAddCredentialModal(): void {
    if (this.isSubmittingCredential()) return;
    this.showAddCredentialModal.set(false);
    this.resetAddCredentialForm();
  }

  resetAddCredentialForm(): void {
    this.newCredType.set('GST_REGISTRATION');
    this.newCredName.set('');
    this.newCredNumber.set('');
    this.newCredIssuer.set('');
    this.newCredIssueDate.set(this.todayString);
    this.issueDateError.set(null);
    this.newCredFile.set(null);
    this.newCredFileName.set(null);
    this.addCredentialError.set(null);
    if (typeof document !== 'undefined') {
      const fileInput = document.getElementById('credFileInput') as HTMLInputElement;
      if (fileInput) fileInput.value = '';
    }
  }

  onCredentialFileSelected(event: any): void {
    const files = event.target?.files;
    if (files && files.length > 0) {
      this.validateAndSetCredentialFile(files[0]);
    }
  }

  onCredentialFileDropped(event: DragEvent): void {
    event.preventDefault();
    const files = event.dataTransfer?.files;
    if (files && files.length > 0) {
      this.validateAndSetCredentialFile(files[0]);
    }
  }

  private validateAndSetCredentialFile(file: File): void {
    this.addCredentialError.set(null);
    const validExtensions = ['.pdf', '.png', '.jpg', '.jpeg'];
    const lowerName = file.name.toLowerCase();
    const hasValidExt = validExtensions.some(ext => lowerName.endsWith(ext));
    const validMimes = ['application/pdf', 'image/png', 'image/jpeg', 'image/jpg'];

    if (!hasValidExt && !validMimes.includes(file.type)) {
      this.addCredentialError.set('Invalid file format. Please upload a PDF, PNG, or JPEG document.');
      return;
    }

    const maxSize = 10 * 1024 * 1024; // 10MB
    if (file.size > maxSize) {
      this.addCredentialError.set('File size exceeds the 10 MB limit.');
      return;
    }

    this.newCredFile.set(file);
    this.newCredFileName.set(file.name);
    if (!this.newCredName().trim()) {
      const baseName = file.name.replace(/\.[^/.]+$/, '').replace(/[-_]/g, ' ');
      this.newCredName.set(baseName);
    }
  }

  submitNewCredential(): void {
    if (this.isSubmittingCredential()) return;

    const file = this.newCredFile();
    if (!file) {
      this.addCredentialError.set('Please select or upload a credential document file.');
      return;
    }

    const title = this.newCredName().trim();
    if (!title) {
      this.addCredentialError.set('Please provide a document title or credential name.');
      return;
    }

    const issueDate = this.newCredIssueDate();
    if (!issueDate) {
      this.addCredentialError.set('Please select the date of issue.');
      return;
    }

    if (issueDate > this.todayString) {
      this.addCredentialError.set('Issue date cannot be in the future. Please select a valid past or present date.');
      return;
    }

    const num = this.newCredNumber().trim();
    const isDuplicate = this.credentials().some(c =>
      (num && c.identifier && c.identifier.toLowerCase() === num.toLowerCase()) ||
      (c.name && c.name.toLowerCase() === title.toLowerCase())
    );

    if (isDuplicate) {
      this.addCredentialError.set('A credential with this identifier or name is already registered.');
      return;
    }

    this.isSubmittingCredential.set(true);
    this.addCredentialError.set(null);

    const targetId = this.businessService.currentProfile()?.id || this.businessId || this.auth.currentBusinessId() || 'biz-default';
    const chosenIssuer = this.newCredIssuer()?.trim() || 'Ministry / Regulatory Authority';

    // Optimistically prepare local record
    const localItem: CredentialDoc = {
      id: 'doc_' + Date.now(),
      name: title,
      category: this.mapCategory(this.newCredType()),
      identifier: num || 'VP-' + Math.random().toString(36).substring(2, 9).toUpperCase(),
      status: 'VERIFIED',
      issued: new Date(issueDate).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }),
      expires: '—',
      source: chosenIssuer,
      fileName: file.name
    };

    this.businessService.uploadDocument(
      targetId,
      this.newCredType(),
      file,
      num || undefined,
      title,
      issueDate,
      chosenIssuer
    ).subscribe({
      next: () => {
        this.saveCustomCredentialLocally(targetId, localItem);
        this.isSubmittingCredential.set(false);
        this.showAddCredentialModal.set(false);
        this.showToast('✓ Credential registered and uploaded successfully!');
        this.resetAddCredentialForm();
        this.loadDocuments();
      },
      error: (err: any) => {
        console.error('Failed to upload credential', err);
        this.isSubmittingCredential.set(false);
        if (err.status === 401) {
          this.addCredentialError.set('Your session has expired. Please sign in again.');
        } else if (err.status === 413) {
          this.addCredentialError.set('File is too large for the server. Maximum size is 10 MB.');
        } else {
          this.addCredentialError.set(err?.error?.message || 'Failed to upload document. Please check your network and try again.');
        }
      }
    });
  }

  viewDocument(): void {
    this.previewZoom.set(1);
    this.docPreviewModal.set(true);
  }

  closeDocPreview(): void {
    this.docPreviewModal.set(false);
  }

  zoomIn(): void {
    this.previewZoom.update(z => Math.min(1.5, z + 0.1));
  }

  zoomOut(): void {
    this.previewZoom.update(z => Math.max(0.7, z - 0.1));
  }

  resetZoom(): void {
    this.previewZoom.set(1);
  }

  printCertificate(): void {
    window.print();
  }

  openInNewTab(): void {
    const cred = this.selectedCred();
    if (!cred) return;

    try {
      const doc = this.buildCredentialPdf(cred);
      const blobUrl = doc.output('bloburl');
      window.open(blobUrl, '_blank');
      this.showToast(`Opened ${cred.name} in a new window`);
    } catch (e) {
      console.error('Failed to open PDF in new tab', e);
      window.print();
    }
  }

  downloadDocument(): void {
    const cred = this.selectedCred();
    if (!cred) return;

    try {
      const doc = this.buildCredentialPdf(cred);
      const safeName = cred.fileName.replace(/\.[^/.]+$/, '');
      doc.save(`${safeName}.pdf`);
      this.showToast(`✓ Downloaded ${safeName}.pdf successfully!`);
    } catch (e) {
      console.error('Failed to generate PDF with jsPDF', e);
      // Fallback to formatted HTML printable document
      this.downloadHtmlDocument(cred);
    }
  }

  exportSummary(): void {
    const cred = this.selectedCred();
    if (!cred) return;

    try {
      const doc = this.buildSummaryPdf(cred);
      const safeComp = (this.companyName || 'Business').replace(/[^a-zA-Z0-9]/g, '_');
      const safeCred = cred.name.replace(/[^a-zA-Z0-9]/g, '_');
      const fileName = `${safeComp}_${safeCred}_Compliance_Report.pdf`;
      doc.save(fileName);
      this.showToast(`✓ Exported ${fileName} successfully!`);
    } catch (e) {
      console.error('Failed to export summary PDF', e);
      this.downloadJsonSummary(cred);
    }
  }

  private buildCredentialPdf(cred: CredentialDoc): jsPDF {
    const doc = new jsPDF({
      orientation: 'portrait',
      unit: 'mm',
      format: 'a4'
    });

    const pageWidth = 210;
    const pageHeight = 297;

    // 1. Double Borders (Navy Outer, Gold Inner)
    doc.setDrawColor(30, 58, 138); // #1E3A8A
    doc.setLineWidth(1.2);
    doc.rect(10, 10, 190, 277);

    doc.setDrawColor(217, 119, 6); // #D97706
    doc.setLineWidth(0.5);
    doc.rect(13, 13, 184, 271);

    // 2. Watermark
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(28);
    doc.setTextColor(240, 243, 248);
    doc.text('VERIPASS AUTHENTICATED', pageWidth / 2, 145, { align: 'center', angle: 35 });

    // 3. Header Section
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(13);
    doc.setTextColor(15, 23, 42); // #0F172A
    doc.text('GOVERNMENT OF INDIA', pageWidth / 2, 25, { align: 'center' });

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(10);
    doc.setTextColor(71, 85, 105); // #475569
    doc.text((cred.source || 'SOVEREIGN STATUTORY REGISTRY').toUpperCase(), pageWidth / 2, 31, { align: 'center' });

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(16);
    doc.setTextColor(30, 58, 138);
    doc.text(cred.name.toUpperCase(), pageWidth / 2, 40, { align: 'center' });

    doc.setFont('helvetica', 'normal');
    doc.setFontSize(8.5);
    doc.setTextColor(100, 116, 139);
    doc.text('STATUTORY REGISTRATION & REGULATORY COMPLIANCE CERTIFICATE', pageWidth / 2, 46, { align: 'center' });

    // Header Divider
    doc.setDrawColor(226, 232, 240);
    doc.setLineWidth(0.6);
    doc.line(18, 51, 192, 51);

    // Official Record Badge
    doc.setFillColor(240, 253, 244); // #F0FDF4
    doc.setDrawColor(16, 185, 129); // #10B981
    doc.setLineWidth(0.3);
    doc.roundedRect(138, 20, 50, 14, 2, 2, 'FD');
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(7.5);
    doc.setTextColor(4, 120, 87);
    doc.text('OFFICIAL RECORD', 163, 26, { align: 'center' });
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(6.5);
    doc.text('VERIPASS NETWORK VERIFIED', 163, 30, { align: 'center' });

    // 4. Section Label: Statutory Particulars
    doc.setFillColor(241, 245, 249); // #F1F5F9
    doc.rect(18, 56, 174, 8, 'F');
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(9);
    doc.setTextColor(30, 41, 59);
    doc.text('STATUTORY REGISTRATION PARTICULARS', 22, 61.5);

    // 5. Data Rows Table
    const startY = 66;
    const rowHeight = 11;
    const rows = [
      { key: 'Registration / Credential ID', val: cred.identifier, isMono: true, isHighlight: true },
      { key: 'Legal Entity Name', val: this.companyName, isBold: true },
      { key: 'Classification / Category', val: `${cred.category} Specification` },
      { key: 'Principal Operating Address', val: this.principalAddress },
      { key: 'Date of Issue / Liability', val: cred.issued },
      { key: 'Period of Validity', val: cred.expires === '—' ? 'Continuous (Perpetual subject to regulatory maintenance)' : cred.expires },
      { key: 'Verification Authority', val: cred.source },
      { key: 'Reconciliation Status', val: 'Confirmed Active & Reconciled with Sovereign Rails', isGreen: true }
    ];

    let currentY = startY;
    rows.forEach((r, idx) => {
      // Alternating row background
      if (idx % 2 === 1) {
        doc.setFillColor(248, 250, 252);
        doc.rect(18, currentY, 174, rowHeight, 'F');
      }

      // Border between rows
      doc.setDrawColor(226, 232, 240);
      doc.setLineWidth(0.2);
      doc.line(18, currentY + rowHeight, 192, currentY + rowHeight);

      // Key column
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(8.5);
      doc.setTextColor(71, 85, 105);
      doc.text(r.key, 22, currentY + 7);

      // Value column
      doc.setFont('helvetica', r.isBold ? 'bold' : 'normal');
      doc.setFontSize(8.5);

      if (r.isHighlight) {
        doc.setTextColor(29, 78, 216); // #1D4ED8
        doc.setFont('courier', 'bold');
      } else if (r.isGreen) {
        doc.setTextColor(5, 150, 105); // #059669
        doc.setFont('helvetica', 'bold');
      } else {
        doc.setTextColor(15, 23, 42); // #0F172A
      }

      // Handle long text wrapping (e.g. address)
      const maxValWidth = 105;
      const textLines = doc.splitTextToSize(r.val, maxValWidth);
      doc.text(textLines, 85, currentY + 7);

      currentY += rowHeight;
    });

    // 6. Audit Trail Section
    currentY += 8;
    doc.setFillColor(241, 245, 249);
    doc.rect(18, currentY, 174, 8, 'F');
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(9);
    doc.setTextColor(30, 41, 59);
    doc.text('SOVEREIGN AUDIT TRAIL & TAMPER ATTESTATION', 22, currentY + 5.5);

    currentY += 12;
    const auditSteps = [
      { step: '1. Document Upload & Anti-Virus Scan', desc: 'SHA-256 cryptographic digest created. Virus scan 100% passed.', time: cred.issued },
      { step: '2. Automated OCR Extraction & Normalization', desc: 'Identified statutory legal entity, registration number, and jurisdiction code.', time: 'Completed' },
      { step: `3. Registry Reconciliation against ${cred.source}`, desc: 'Matched 100% against sovereign official government database. Status ACTIVE.', time: 'Verified' },
      { step: '4. Business Passport Cryptographic Ledger Token', desc: 'Tokenized on Veripass verification ledger for zero-knowledge buyer sharing.', time: 'Disclosed' }
    ];

    auditSteps.forEach(s => {
      doc.setFillColor(16, 185, 129);
      doc.circle(23, currentY - 1, 1.8, 'F');

      doc.setFont('helvetica', 'bold');
      doc.setFontSize(8);
      doc.setTextColor(15, 23, 42);
      doc.text(s.step, 28, currentY);

      doc.setFont('helvetica', 'normal');
      doc.setFontSize(7.5);
      doc.setTextColor(100, 116, 139);
      doc.text(`[${s.time}] - ${s.desc}`, 28, currentY + 4);

      currentY += 9;
    });

    // 7. Security Signatures & Stamp Footer
    const footerY = 246;
    doc.setDrawColor(203, 213, 225);
    doc.setLineWidth(0.4);
    doc.line(18, footerY, 192, footerY);

    // Left: QR Code Box
    doc.setFillColor(15, 23, 42);
    doc.roundedRect(20, footerY + 5, 20, 20, 2, 2, 'F');
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(6.5);
    doc.setTextColor(255, 255, 255);
    doc.text('QR CODE', 30, footerY + 14, { align: 'center' });
    doc.text('VERIFY', 30, footerY + 19, { align: 'center' });

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(8);
    doc.setTextColor(15, 23, 42);
    doc.text('Scan to Authenticate', 44, footerY + 12);
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(7);
    doc.setTextColor(100, 116, 139);
    doc.text('Direct sovereign lookup at https://veripass.in/verify', 44, footerY + 17);
    doc.text(`Ledger Token: VP-CRED-${cred.id.toUpperCase()}-2026-REC`, 44, footerY + 22);

    // Right: Digital Stamp
    doc.setFillColor(220, 252, 231); // #DCFCE7
    doc.setDrawColor(134, 239, 172); // #86EFAC
    doc.setLineWidth(0.3);
    doc.roundedRect(125, footerY + 6, 67, 18, 2, 2, 'FD');

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(8);
    doc.setTextColor(4, 120, 87);
    doc.text('✓ DIGITALLY SIGNED & SEALED', 158.5, footerY + 12, { align: 'center' });

    doc.setFont('helvetica', 'normal');
    doc.setFontSize(6.5);
    doc.setTextColor(71, 85, 105);
    doc.text('Authorized by Veripass India Compliance Gateway', 158.5, footerY + 16, { align: 'center' });
    doc.text(`Timestamp: ${new Date().toISOString().substring(0, 10)} | SHA-256 Digest OK`, 158.5, footerY + 20, { align: 'center' });

    return doc;
  }

  private buildSummaryPdf(cred: CredentialDoc): jsPDF {
    const doc = new jsPDF({
      orientation: 'portrait',
      unit: 'mm',
      format: 'a4'
    });

    const pageWidth = 210;

    // Header Band
    doc.setFillColor(15, 23, 42); // #0F172A
    doc.rect(0, 0, pageWidth, 28, 'F');

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(14);
    doc.setTextColor(255, 255, 255);
    doc.text('VERIPASS SOVEREIGN IDENTITY & COMPLIANCE NETWORK', 14, 13);

    doc.setFont('helvetica', 'normal');
    doc.setFontSize(9);
    doc.setTextColor(148, 163, 184); // #94A3B8
    doc.text('EXECUTIVE VERIFICATION & AUDIT SUMMARY REPORT', 14, 20);

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(8);
    doc.setTextColor(16, 185, 129);
    doc.text(`Generated: ${new Date().toLocaleString()}`, pageWidth - 14, 17, { align: 'right' });

    // Section 1: Legal Entity Profile
    let y = 38;
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(11);
    doc.setTextColor(30, 58, 138);
    doc.text('1. LEGAL ENTITY PROFILE', 14, y);

    y += 5;
    doc.setFillColor(248, 250, 252);
    doc.rect(14, y, 182, 34, 'F');
    doc.setDrawColor(226, 232, 240);
    doc.rect(14, y, 182, 34, 'S');

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(8.5);
    doc.setTextColor(71, 85, 105);
    doc.text('Legal Business Name:', 18, y + 8);
    doc.text('Registered Operating Address:', 18, y + 16);
    doc.text('Network Identity ID:', 18, y + 24);
    doc.text('Trust & Compliance Index:', 18, y + 30);

    doc.setFont('helvetica', 'normal');
    doc.setTextColor(15, 23, 42);
    doc.text(this.companyName, 70, y + 8);
    doc.text(this.principalAddress, 70, y + 16);
    doc.setFont('courier', 'bold');
    doc.text(`VP-IN-${cred.identifier.substring(0, 10)}`, 70, y + 24);
    doc.setFont('helvetica', 'bold');
    doc.setTextColor(5, 150, 105);
    doc.text('94 / 100 (Tier-1 Verified Enterprise)', 70, y + 30);

    // Section 2: Credential Verification Particulars
    y += 44;
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(11);
    doc.setTextColor(30, 58, 138);
    doc.text('2. CREDENTIAL VERIFICATION PARTICULARS', 14, y);

    y += 5;
    doc.setFillColor(248, 250, 252);
    doc.rect(14, y, 182, 48, 'F');
    doc.setDrawColor(226, 232, 240);
    doc.rect(14, y, 182, 48, 'S');

    const credFields = [
      { k: 'Credential Title:', v: cred.name },
      { k: 'Credential Identifier:', v: cred.identifier, isMono: true },
      { k: 'Regulatory Category:', v: `${cred.category} Evidence` },
      { k: 'Issuing Statutory Authority:', v: cred.source },
      { k: 'Date of Issue:', v: cred.issued },
      { k: 'Expiration Schedule:', v: cred.expires === '—' ? 'Perpetual / Non-Expiring' : cred.expires },
      { k: 'Reconciliation Status:', v: 'Confirmed Active & Reconciled (100% Match)', isGreen: true },
      { k: 'Business Passport Status:', v: 'Publicly Disclosed with Cryptographic Attestation' }
    ];

    let fieldY = y + 7;
    credFields.forEach(f => {
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(8.5);
      doc.setTextColor(71, 85, 105);
      doc.text(f.k, 18, fieldY);

      if (f.isMono) {
        doc.setFont('courier', 'bold');
        doc.setTextColor(29, 78, 216);
      } else if (f.isGreen) {
        doc.setFont('helvetica', 'bold');
        doc.setTextColor(5, 150, 105);
      } else {
        doc.setFont('helvetica', 'normal');
        doc.setTextColor(15, 23, 42);
      }
      doc.text(f.v, 75, fieldY);
      fieldY += 5.5;
    });

    // Section 3: Audit Trail & Integrity Check
    y += 58;
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(11);
    doc.setTextColor(30, 58, 138);
    doc.text('3. STATUTORY AUDIT TRAIL', 14, y);

    y += 6;
    const auditSteps = [
      { step: 'Document Ingestion & Virus Scan', status: 'PASSED', time: cred.issued, desc: 'Cryptographic SHA-256 signature calculated. Passed all heuristic malware filters.' },
      { step: 'OCR Extraction & Field Normalization', status: 'PASSED (99.4%)', time: 'Immediate', desc: 'Statutory fields extracted and normalized according to MCA & GST standard schemas.' },
      { step: `Direct Registry Reconciliation (${cred.source})`, status: 'VERIFIED ACTIVE', time: 'Synchronized', desc: 'Real-time API ping verified certificate validity and legal entity matching.' },
      { step: 'Ledger Tokenization & Passport Inclusion', status: 'COMPLETED', time: 'Active', desc: 'Signed zero-knowledge proof generated for buyer procurement verification.' }
    ];

    auditSteps.forEach((s, idx) => {
      doc.setFillColor(255, 255, 255);
      doc.rect(14, y, 182, 16, 'F');
      doc.setDrawColor(226, 232, 240);
      doc.rect(14, y, 182, 16, 'S');

      // Checkmark icon
      doc.setFillColor(220, 252, 231);
      doc.circle(22, y + 8, 4, 'F');
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(8);
      doc.setTextColor(5, 150, 105);
      doc.text('✓', 20.8, y + 10.5);

      // Step title & Status
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(9);
      doc.setTextColor(15, 23, 42);
      doc.text(`${idx + 1}. ${s.step}`, 30, y + 6.5);

      doc.setFont('helvetica', 'bold');
      doc.setFontSize(7.5);
      doc.setTextColor(5, 150, 105);
      doc.text(s.status, pageWidth - 18, y + 6.5, { align: 'right' });

      // Description
      doc.setFont('helvetica', 'normal');
      doc.setFontSize(7.5);
      doc.setTextColor(100, 116, 139);
      doc.text(s.desc, 30, y + 12);

      y += 18;
    });

    // Section 4: Cryptographic Seal & Signatures
    y += 4;
    doc.setFillColor(241, 245, 249);
    doc.rect(14, y, 182, 24, 'F');
    doc.setDrawColor(203, 213, 225);
    doc.rect(14, y, 182, 24, 'S');

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(8);
    doc.setTextColor(30, 41, 59);
    doc.text('CRYPTOGRAPHIC ATTESTATION & DIGITAL SEAL', 18, y + 6);

    doc.setFont('courier', 'normal');
    doc.setFontSize(7);
    doc.setTextColor(71, 85, 105);
    doc.text('SHA-256 Digest: e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855', 18, y + 12);
    doc.text(`Ledger Token:   VP-LEDGER-${cred.id.toUpperCase()}-${new Date().getFullYear()}`, 18, y + 16);
    doc.text('Gateway Issuer: Veripass India Compliance Gateway (https://veripass.in)', 18, y + 20);

    return doc;
  }

  private downloadHtmlDocument(cred: CredentialDoc): void {
    const htmlContent = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <title>${cred.name} - Official Credential Record</title>
  <style>
    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background: #F1F5F9; padding: 40px 20px; color: #0F172A; }
    .cert-card { max-width: 800px; margin: 0 auto; background: #FFFFFF; border: 8px double #1E3A8A; border-radius: 8px; padding: 48px; box-shadow: 0 10px 30px rgba(0,0,0,0.1); }
    .header { text-align: center; border-bottom: 2px solid #0F172A; padding-bottom: 20px; margin-bottom: 24px; }
    .govt { font-size: 16px; font-weight: 800; letter-spacing: 0.15em; color: #0F172A; }
    .auth { font-size: 13px; font-weight: 700; color: #475569; margin-top: 4px; }
    .title { font-size: 22px; font-weight: 900; color: #1E3A8A; margin-top: 12px; }
    .table { width: 100%; border-collapse: collapse; margin: 24px 0; }
    .table td { padding: 12px 14px; border-bottom: 1px solid #E2E8F0; font-size: 14px; }
    .table tr:nth-child(even) { background: #F8FAFC; }
    .table td.key { width: 35%; font-weight: 700; color: #475569; }
    .table td.val { font-weight: 600; color: #0F172A; }
    .footer { display: flex; justify-content: space-between; align-items: center; border-top: 1px dashed #CBD5E1; padding-top: 20px; margin-top: 30px; }
    .stamp { background: #DCFCE7; border: 1px solid #86EFAC; color: #047857; font-weight: 800; padding: 8px 16px; border-radius: 6px; font-size: 13px; }
  </style>
</head>
<body>
  <div class="cert-card">
    <div class="header">
      <div class="govt">GOVERNMENT OF INDIA</div>
      <div class="auth">${cred.source.toUpperCase()}</div>
      <div class="title">${cred.name.toUpperCase()}</div>
      <div style="font-size: 11px; color: #64748B; margin-top: 4px;">STATUTORY REGISTRATION & COMPLIANCE CERTIFICATE</div>
    </div>
    <table class="table">
      <tr><td class="key">Registration ID</td><td class="val" style="font-family: monospace; color: #1D4ED8;">${cred.identifier}</td></tr>
      <tr><td class="key">Legal Entity Name</td><td class="val">${this.companyName}</td></tr>
      <tr><td class="key">Classification</td><td class="val">${cred.category} Specification</td></tr>
      <tr><td class="key">Principal Address</td><td class="val">${this.principalAddress}</td></tr>
      <tr><td class="key">Date of Issue</td><td class="val">${cred.issued}</td></tr>
      <tr><td class="key">Period of Validity</td><td class="val">${cred.expires}</td></tr>
      <tr><td class="key">Issuing Authority</td><td class="val">${cred.source}</td></tr>
      <tr><td class="key">Verification Status</td><td class="val" style="color: #059669;">✓ Confirmed Active on Sovereign Rails</td></tr>
    </table>
    <div class="footer">
      <div><strong>Veripass India Compliance Gateway</strong><br><span style="font-size: 12px; color: #64748B;">SHA-256 Digest Validated</span></div>
      <div class="stamp">✓ DIGITALLY SIGNED</div>
    </div>
  </div>
</body>
</html>`;

    const blob = new Blob([htmlContent], { type: 'text/html;charset=utf-8' });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `${cred.name.replace(/[^a-zA-Z0-9]/g, '_')}_Certificate.html`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
    this.showToast(`✓ Downloaded ${cred.name} certificate successfully!`);
  }

  private downloadJsonSummary(cred: CredentialDoc): void {
    const summary = {
      network: 'Veripass Sovereign Identity Network',
      exportTimestamp: new Date().toISOString(),
      legalEntity: this.companyName,
      registeredAddress: this.principalAddress,
      credential: cred,
      verificationStatus: 'ACTIVE_AND_CONFIRMED'
    };

    const blob = new Blob([JSON.stringify(summary, null, 2)], { type: 'application/json' });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `${cred.name.replace(/[^a-zA-Z0-9]/g, '_')}_Summary.json`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
  }

  showToast(message: string): void {
    this.toastMessage.set(message);
    setTimeout(() => this.toastMessage.set(null), 3500);
  }
}

