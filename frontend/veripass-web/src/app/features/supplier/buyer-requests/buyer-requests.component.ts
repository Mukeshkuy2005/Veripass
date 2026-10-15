import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { BusinessService } from '../../../core/services/business.service';
import { VerificationRequestDto } from '../../../core/models/models';

interface CredentialMeta {
  code: string;
  name: string;
  category: string;
  icon: string;
}

@Component({
  selector: 'app-buyer-requests',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './buyer-requests.component.html',
  styleUrl: './buyer-requests.component.css'
})
export class BuyerRequestsComponent implements OnInit {
  private businessService = inject(BusinessService);

  // Reactive state from global BusinessService
  requests = this.businessService.verificationRequests;
  isLoading = signal<boolean>(false);
  isSubmitting = signal<boolean>(false);

  // Filters & Search
  filterStatus = signal<'ALL' | 'PENDING' | 'APPROVED' | 'REJECTED'>('ALL');
  searchQuery = signal<string>('');
  selectedType = signal<string>('ALL');

  // Modals
  activeReviewReq = signal<VerificationRequestDto | null>(null);
  activeRejectReq = signal<VerificationRequestDto | null>(null);
  rejectReasonText = signal<string>('');

  // Consent Configuration in Modal
  shareRawDoc = signal<boolean>(false);
  selectedCredentials = signal<{ [key: string]: boolean }>({});
  validityDays = signal<number>(30);
  consentNotes = signal<string>('');

  // Toast feedback
  toastMessage = signal<string | null>(null);
  toastType = signal<'success' | 'error' | 'info'>('info');

  // Metadata mappings
  readonly credentialDirectory: { [key: string]: CredentialMeta } = {
    'GST': { code: 'GST', name: 'Goods & Services Tax (GSTIN)', category: 'TAX', icon: '🏛' },
    'PAN': { code: 'PAN', name: 'Permanent Account Number (PAN)', category: 'STATUTORY', icon: '🪪' },
    'CIN': { code: 'CIN', name: 'Corporate Identification (CIN)', category: 'STATUTORY', icon: '🏢' },
    'UDYAM': { code: 'UDYAM', name: 'MSME Udyam Registration', category: 'MSME', icon: '📜' },
    'BANK_ACCOUNT': { code: 'BANK_ACCOUNT', name: 'Bank Penny-Drop Attestation', category: 'FINANCIAL', icon: '🏦' },
    'ISO_9001': { code: 'ISO_9001', name: 'ISO 9001:2015 Quality Management', category: 'QUALITY', icon: '🏅' },
    'TRADE_LICENSE': { code: 'TRADE_LICENSE', name: 'Municipal Trade License', category: 'STATUTORY', icon: '📑' },
    'INSURANCE': { code: 'INSURANCE', name: 'Liability & Workman Insurance', category: 'SAFETY', icon: '🛡' }
  };

  // KPI Metrics directly tied to global store
  totalCount = this.businessService.totalBuyerRequestsCount;
  pendingCount = this.businessService.pendingBuyerRequestsCount;
  approvedCount = this.businessService.approvedBuyerRequestsCount;
  rejectedCount = this.businessService.rejectedBuyerRequestsCount;

  // Filtered requests computed
  filteredRequests = computed(() => {
    let list = this.requests();
    const status = this.filterStatus();
    const query = this.searchQuery().trim().toLowerCase();
    const type = this.selectedType();

    if (status !== 'ALL') {
      list = list.filter(r => r.status === status);
    }

    if (type !== 'ALL') {
      list = list.filter(r => r.requestType === type);
    }

    if (query) {
      list = list.filter(r =>
        r.buyerOrganizationName.toLowerCase().includes(query) ||
        (r.reason && r.reason.toLowerCase().includes(query)) ||
        r.requestedItems.some(i => i.toLowerCase().includes(query)) ||
        r.id.toLowerCase().includes(query)
      );
    }

    return list;
  });

  ngOnInit(): void {
    this.loadRequests();
  }

  loadRequests(): void {
    this.isLoading.set(true);
    this.businessService.getVerificationRequests().subscribe({
      next: (data) => {
        this.requests.set(data || []);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load verification requests:', err);
        this.isLoading.set(false);
        this.showToast('Failed to connect to backend server. Using local cache.', 'error');
      }
    });
  }

  // Credential helper methods
  getCredInfo(code: string): CredentialMeta {
    return this.credentialDirectory[code] || {
      code,
      name: code.replace(/_/g, ' '),
      category: 'CREDENTIAL',
      icon: '✓'
    };
  }

  getRequestTypeBadge(type: string): { label: string; class: string } {
    switch (type) {
      case 'VENDOR_ONBOARDING':
        return { label: 'Vendor Onboarding', class: 'type-onboarding' };
      case 'CREDENTIAL_UPDATE':
        return { label: 'Credential Refresh', class: 'type-update' };
      case 'AUDIT':
        return { label: 'Statutory Audit', class: 'type-audit' };
      case 'RE_VERIFY':
        return { label: 'Annual Re-Verify', class: 'type-reverify' };
      default:
        return { label: type.replace(/_/g, ' '), class: 'type-default' };
    }
  }

  getBuyerInitials(name: string): string {
    if (!name) return 'BY';
    const words = name.replace(/[()]/g, '').trim().split(/\s+/);
    if (words.length >= 2) {
      return (words[0][0] + words[1][0]).toUpperCase();
    }
    return name.substring(0, 2).toUpperCase();
  }

  getBuyerAvatarColor(name: string): string {
    const colors = [
      'linear-gradient(135deg, #1E3A8A, #3B82F6)',
      'linear-gradient(135deg, #065F46, #10B981)',
      'linear-gradient(135deg, #7C2D12, #F59E0B)',
      'linear-gradient(135deg, #4C1D95, #8B5CF6)',
      'linear-gradient(135deg, #831843, #EC4899)'
    ];
    let hash = 0;
    for (let i = 0; i < name.length; i++) {
      hash = name.charCodeAt(i) + ((hash << 5) - hash);
    }
    const index = Math.abs(hash) % colors.length;
    return colors[index];
  }

  formatDate(dateStr: string): string {
    if (!dateStr) return '';
    try {
      const d = new Date(dateStr);
      return d.toLocaleDateString('en-IN', {
        day: '2-digit',
        month: 'short',
        year: 'numeric'
      });
    } catch {
      return dateStr;
    }
  }

  // Open Consent Gateway Modal
  openReviewModal(req: VerificationRequestDto): void {
    this.activeReviewReq.set(req);
    this.shareRawDoc.set(false);
    this.validityDays.set(30);
    this.consentNotes.set(`Sovereign DPDP consent granted to ${req.buyerOrganizationName} for verified credential package.`);

    // Initialize all requested items as selected
    const initialMap: { [key: string]: boolean } = {};
    req.requestedItems.forEach(item => {
      initialMap[item] = true;
    });
    this.selectedCredentials.set(initialMap);
  }

  closeReviewModal(): void {
    this.activeReviewReq.set(null);
  }

  toggleCredential(item: string): void {
    const current = { ...this.selectedCredentials() };
    current[item] = !current[item];
    this.selectedCredentials.set(current);
  }

  hasSelectedCredentials(): boolean {
    const current = this.selectedCredentials();
    return Object.values(current).some(v => v);
  }

  getSelectedCount(): number {
    return Object.values(this.selectedCredentials()).filter(Boolean).length;
  }

  // Open Reject Modal
  openRejectModal(req: VerificationRequestDto): void {
    this.activeRejectReq.set(req);
    this.rejectReasonText.set('');
  }

  closeRejectModal(): void {
    this.activeRejectReq.set(null);
    this.rejectReasonText.set('');
  }

  switchReviewToReject(): void {
    const current = this.activeReviewReq();
    this.closeReviewModal();
    if (current) {
      this.openRejectModal(current);
    }
  }

  // Execute Approval
  confirmApprove(): void {
    const req = this.activeReviewReq();
    if (!req) return;

    this.isSubmitting.set(true);
    const mode = this.shareRawDoc() ? 'Verified Attestation + Redacted Document Proof' : 'Zero-Knowledge Verified Attestation Only';
    const notes = this.consentNotes().trim() || `Approved. Access Mode: ${mode}. Validity: ${this.validityDays()} days.`;

    this.businessService.approveVerificationRequest(req.id, notes).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.closeReviewModal();
        this.showToast(`Access granted to ${req.buyerOrganizationName}. Cryptographic token issued.`, 'success');
      },
      error: (err) => {
        console.error('Approve failed:', err);
        this.isSubmitting.set(false);
        this.showToast('Failed to record approval. Please try again.', 'error');
      }
    });
  }

  // Execute Rejection
  confirmReject(): void {
    const req = this.activeRejectReq();
    if (!req) return;

    this.isSubmitting.set(true);
    const reason = this.rejectReasonText().trim() || 'Access declined by supplier compliance administrator.';

    this.businessService.rejectVerificationRequest(req.id, reason).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.closeRejectModal();
        this.showToast(`Request from ${req.buyerOrganizationName} declined.`, 'info');
      },
      error: (err) => {
        console.error('Reject failed:', err);
        this.isSubmitting.set(false);
        this.showToast('Failed to record rejection. Please try again.', 'error');
      }
    });
  }

  // Quick Approve directly from card
  quickApprove(req: VerificationRequestDto, event: Event): void {
    event.stopPropagation();
    this.activeReviewReq.set(req);
    this.openReviewModal(req);
  }

  showToast(message: string, type: 'success' | 'error' | 'info' = 'info'): void {
    this.toastMessage.set(message);
    this.toastType.set(type);
    setTimeout(() => {
      this.toastMessage.set(null);
    }, 4500);
  }
}
