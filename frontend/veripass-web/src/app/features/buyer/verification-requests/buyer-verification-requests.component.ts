import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { AuthService } from '../../../core/auth/auth.service';
import { BuyerSearchService, BuyerSupplier } from '../../../core/services/buyer-search.service';
import { VerificationRequestDto, CreateVerificationRequestPayload } from '../../../core/models/models';

@Component({
  selector: 'app-buyer-verification-requests',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './buyer-verification-requests.component.html',
  styleUrl: './buyer-verification-requests.component.css'
})
export class BuyerVerificationRequestsComponent implements OnInit {
  private http = inject(HttpClient);
  private auth = inject(AuthService);
  private searchService = inject(BuyerSearchService);
  private readonly apiUrl = 'http://localhost:5000/api/v1';

  // Primary State Signals
  requests = signal<VerificationRequestDto[]>([]);
  loading = signal<boolean>(true);
  errorMessage = signal<string | null>(null);
  toastMessage = signal<string | null>(null);
  toastType = signal<'success' | 'info' | 'error'>('success');
  copiedValue = signal<string | null>(null);

  // Search & Filter Signals
  searchTerm = signal<string>('');
  statusFilter = signal<'ALL' | 'PENDING' | 'APPROVED' | 'REJECTED' | 'CANCELLED'>('ALL');
  typeFilter = signal<string>('ALL');
  sortBy = signal<'NEWEST' | 'OLDEST' | 'SUPPLIER_ASC' | 'SCORE_DESC'>('NEWEST');

  // Modal Signals
  showCreateModal = signal<boolean>(false);
  submitting = signal<boolean>(false);
  selectedDetailRequest = signal<VerificationRequestDto | null>(null);

  // Available Suppliers Directory (for dropdown)
  availableSuppliers = signal<BuyerSupplier[]>([]);

  // Create Request Form State
  createForm = {
    supplierBusinessId: '',
    requestType: 'VENDOR_ONBOARDING',
    artifacts: {
      bank: true,
      gst: true,
      iso: false,
      financials: false,
      factory: false,
      insurance: false
    },
    reason: 'Statutory credential verification and NPCI penny-drop for automated ERP vendor master registration.',
    slaDays: '7'
  };

  // Executive KPI Telemetry
  totalCount = computed(() => this.requests().length);
  pendingCount = computed(() => this.requests().filter(r => r.status === 'PENDING').length);
  approvedCount = computed(() => this.requests().filter(r => r.status === 'APPROVED').length);
  rejectedCount = computed(() => this.requests().filter(r => r.status === 'REJECTED' || r.status === 'CANCELLED').length);

  // SLA Turnaround Telemetry
  slaMetrics = computed(() => {
    const completed = this.requests().filter(r => (r.status === 'APPROVED' || r.status === 'REJECTED') && r.respondedAt && r.createdAt);
    if (completed.length === 0) return null;

    let totalHours = 0;
    let validCount = 0;
    let withinTargetCount = 0;
    const targetDays = 7;
    const targetHours = targetDays * 24;

    for (const r of completed) {
      const createdTime = new Date(r.createdAt).getTime();
      const respondedTime = new Date(r.respondedAt!).getTime();
      if (isNaN(createdTime) || isNaN(respondedTime)) continue;

      const diffMs = Math.max(0, respondedTime - createdTime);
      const hours = diffMs / (1000 * 60 * 60);
      totalHours += hours;
      validCount++;
      if (hours <= targetHours) {
        withinTargetCount++;
      }
    }

    if (validCount === 0) return null;

    const avgHours = totalHours / validCount;
    const onTrackPercent = Math.round((withinTargetCount / validCount) * 100);

    if (avgHours < 24) {
      return {
        value: Math.max(0.5, Math.round(avgHours * 10) / 10),
        unit: 'hours',
        target: '24 hours',
        onTrackPercent: onTrackPercent,
        completedCount: validCount
      };
    } else {
      return {
        value: Math.round((avgHours / 24) * 10) / 10,
        unit: 'days',
        target: '7 days',
        onTrackPercent: onTrackPercent,
        completedCount: validCount
      };
    }
  });

  // Filtered & Sorted Display Pipeline
  filteredRequests = computed<VerificationRequestDto[]>(() => {
    let list = this.requests();
    const query = this.searchTerm().trim().toLowerCase();
    const status = this.statusFilter();
    const type = this.typeFilter();
    const sort = this.sortBy();

    // 1. Status Filter
    if (status !== 'ALL') {
      list = list.filter(r => r.status === status);
    }

    // 2. Type Filter
    if (type !== 'ALL') {
      list = list.filter(r => r.requestType === type);
    }

    // 3. Search Query Filter
    if (query) {
      list = list.filter(r => {
        const idMatch = r.id.toLowerCase().includes(query);
        const nameMatch = (r.supplierBusinessName || '').toLowerCase().includes(query);
        const tradeMatch = (r.supplierTradeName || '').toLowerCase().includes(query);
        const gstinMatch = (r.supplierGstin || '').toLowerCase().includes(query);
        const cityMatch = (r.supplierCity || '').toLowerCase().includes(query);
        const reasonMatch = (r.reason || '').toLowerCase().includes(query);
        const typeMatch = (r.requestType || '').toLowerCase().includes(query);
        const itemsMatch = (r.requestedItems || []).some(i => i.toLowerCase().includes(query));

        return idMatch || nameMatch || tradeMatch || gstinMatch || cityMatch || reasonMatch || typeMatch || itemsMatch;
      });
    }

    // 4. Sorting
    return [...list].sort((a, b) => {
      if (sort === 'NEWEST') {
        return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
      } else if (sort === 'OLDEST') {
        return new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();
      } else if (sort === 'SUPPLIER_ASC') {
        const nameA = a.supplierTradeName || a.supplierBusinessName || '';
        const nameB = b.supplierTradeName || b.supplierBusinessName || '';
        return nameA.localeCompare(nameB);
      } else if (sort === 'SCORE_DESC') {
        return (b.supplierTrustScore || 0) - (a.supplierTrustScore || 0);
      }
      return 0;
    });
  });

  ngOnInit(): void {
    this.loadSuppliersDirectory();
    this.loadRequests();
  }

  // Load Verified Suppliers Directory
  loadSuppliersDirectory(): void {
    const list = this.searchService.getAllSuppliers();
    this.availableSuppliers.set(list);
    if (list.length > 0 && !this.createForm.supplierBusinessId) {
      this.createForm.supplierBusinessId = list[0].id;
    }
  }

  // Fetch Verification Requests from Live Backend API
  loadRequests(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    const orgId = this.auth.currentUser()?.currentOrganizationId;
    if (!orgId) {
      this.requests.set([]);
      this.loading.set(false);
      return;
    }

    const url = `${this.apiUrl}/verification-requests?buyerOrganizationId=${orgId}`;
    this.http.get<VerificationRequestDto[]>(url).subscribe({
      next: (data) => {
        const enhanced = (data || []).map(item => this.enrichRequestWithSupplierInfo(item));
        this.requests.set(enhanced);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to fetch verification requests from API', err);
        this.requests.set([]);
        this.loading.set(false);
      }
    });
  }

  private enrichRequestWithSupplierInfo(req: VerificationRequestDto): VerificationRequestDto {
    const sup = this.searchService.getSupplierById(req.supplierBusinessId);
    return {
      ...req,
      supplierBusinessName: req.supplierBusinessName || sup?.legalName || sup?.name || 'Enterprise Supplier',
      supplierTradeName: req.supplierTradeName || sup?.name || 'Enterprise Supplier',
      supplierCity: req.supplierCity || sup?.location || '',
      supplierState: req.supplierState || sup?.state || '',
      supplierGstin: req.supplierGstin || sup?.gstin || '—',
      supplierTrustScore: req.supplierTrustScore ?? sup?.trustScore ?? 0
    };
  }

  // Avatar Color Palette Generator
  getAvatarBg(name: string | undefined): { bg: string; color: string } {
    const colors = [
      { bg: '#EFF6FF', color: '#1D4ED8' }, // Blue
      { bg: '#ECFDF5', color: '#047857' }, // Emerald
      { bg: '#F5F3FF', color: '#6D28D9' }, // Purple
      { bg: '#FFF7ED', color: '#C2410C' }, // Orange
      { bg: '#FDF2F8', color: '#BE185D' }, // Pink
      { bg: '#F0FDFA', color: '#0F766E' }  // Teal
    ];
    if (!name) return colors[0];
    let hash = 0;
    for (let i = 0; i < name.length; i++) {
      hash = name.charCodeAt(i) + ((hash << 5) - hash);
    }
    const idx = Math.abs(hash) % colors.length;
    return colors[idx];
  }

  // Safe Date Formatter
  formatDate(dateStr?: string): string {
    if (!dateStr) return '—';
    const timestamp = new Date(dateStr).getTime();
    if (isNaN(timestamp)) return '—';
    const d = new Date(timestamp);
    const day = String(d.getDate()).padStart(2, '0');
    const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    return `${day} ${months[d.getMonth()]} ${d.getFullYear()}`;
  }

  // Safe Date & Time Formatter
  formatDateTime(dateStr?: string): string {
    if (!dateStr) return '—';
    const timestamp = new Date(dateStr).getTime();
    if (isNaN(timestamp)) return '—';
    const d = new Date(timestamp);
    const day = String(d.getDate()).padStart(2, '0');
    const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    const hours = String(d.getHours()).padStart(2, '0');
    const minutes = String(d.getMinutes()).padStart(2, '0');
    return `${day} ${months[d.getMonth()]} ${d.getFullYear()}, ${hours}:${minutes}`;
  }

  // Relative Time Formatter
  getRelativeTime(dateStr?: string): string {
    if (!dateStr) return '';
    const now = Date.now();
    const then = new Date(dateStr).getTime();
    if (isNaN(then)) return '';
    const diffSec = Math.floor((now - then) / 1000);
    if (diffSec < 60) return 'Just now';
    const diffMin = Math.floor(diffSec / 60);
    if (diffMin < 60) return `${diffMin}m ago`;
    const diffHours = Math.floor(diffMin / 60);
    if (diffHours < 24) return `${diffHours}h ago`;
    const diffDays = Math.floor(diffHours / 24);
    if (diffDays === 1) return 'Yesterday';
    if (diffDays < 30) return `${diffDays}d ago`;
    const diffMonths = Math.floor(diffDays / 30);
    return `${diffMonths}mo ago`;
  }

  // Category Badge Class Helper
  getCategoryBadgeClass(type: string): string {
    switch (type) {
      case 'VENDOR_ONBOARDING': return 'cat-onboarding';
      case 'AUDIT': return 'cat-audit';
      case 'CREDENTIAL_UPDATE': return 'cat-renewal';
      case 'GEO_AUDIT': return 'cat-geo';
      case 'RE_VERIFY': return 'cat-reverify';
      default: return 'cat-default';
    }
  }

  // Compact Artifacts Summary
  formatArtifactsSummary(items: string[] | undefined): string {
    if (!items || items.length === 0) return 'Standard Verification';
    const formatted = items.map(i => this.formatArtifact(i));
    if (formatted.length <= 2) {
      return formatted.join(', ');
    }
    return `${formatted[0]}, ${formatted[1]} +${formatted.length - 2} more`;
  }

  // Fallback Data if backend connection fails
  private loadSampleFallback(): void {
    this.requests.set([]);
  }

  // Quick preset text helper for modal
  onTypeChanged(type: string): void {
    if (type === 'VENDOR_ONBOARDING') {
      this.createForm.artifacts.bank = true;
      this.createForm.artifacts.gst = true;
      this.createForm.artifacts.iso = false;
      this.createForm.artifacts.financials = false;
      this.createForm.reason = 'Statutory credential verification and NPCI penny-drop for automated ERP vendor master registration.';
    } else if (type === 'AUDIT') {
      this.createForm.artifacts.financials = true;
      this.createForm.artifacts.bank = true;
      this.createForm.artifacts.gst = true;
      this.createForm.reason = 'High-value tender prequalification — last 3 years audited balance sheets (MCA) and bank solvency proof.';
    } else if (type === 'CREDENTIAL_UPDATE') {
      this.createForm.artifacts.iso = true;
      this.createForm.artifacts.gst = true;
      this.createForm.artifacts.bank = false;
      this.createForm.reason = 'Annual statutory compliance renewal. Verified ISO 9001:2015 registrar certificate and active tax filing return telemetry.';
    } else if (type === 'GEO_AUDIT') {
      this.createForm.artifacts.factory = true;
      this.createForm.artifacts.iso = false;
      this.createForm.reason = 'Physical plant geo-boundary validation and premises industrial safety protocol inspection.';
    } else if (type === 'RE_VERIFY') {
      this.createForm.artifacts.insurance = true;
      this.createForm.artifacts.gst = true;
      this.createForm.reason = 'Transit liability insurance policy and active statutory trade license reverification.';
    }
  }

  // Open & Close Modals
  openCreateModal(): void {
    this.showCreateModal.set(true);
  }

  closeCreateModal(): void {
    this.showCreateModal.set(false);
  }

  viewDetails(req: VerificationRequestDto): void {
    this.selectedDetailRequest.set(req);
  }

  closeDetails(): void {
    this.selectedDetailRequest.set(null);
  }

  // Create Request Action (Calls Backend API)
  submitCreateRequest(): void {
    if (!this.createForm.supplierBusinessId) {
      this.showToast('Please select a target supplier.', 'error');
      return;
    }

    const items: string[] = [];
    if (this.createForm.artifacts.bank) items.push('BANK_ACCOUNT');
    if (this.createForm.artifacts.gst) items.push('GST');
    if (this.createForm.artifacts.iso) items.push('ISO_9001');
    if (this.createForm.artifacts.financials) items.push('MCA_FINANCIALS');
    if (this.createForm.artifacts.factory) items.push('GEO_LOCATION');
    if (this.createForm.artifacts.insurance) items.push('INSURANCE');

    if (items.length === 0) {
      items.push('STATUTORY_CLEARANCE');
    }

    const orgId = this.auth.currentUser()?.currentOrganizationId;
    if (!orgId) {
      this.showToast('Authentication session error: missing organization context.', 'error');
      return;
    }

    this.submitting.set(true);

    const payload: CreateVerificationRequestPayload = {
      buyerOrganizationId: orgId,
      supplierBusinessId: this.createForm.supplierBusinessId,
      requestType: this.createForm.requestType,
      requestedItems: items,
      reason: this.createForm.reason
    };

    this.http.post<{ requestId: string }>(`${this.apiUrl}/verification-requests`, payload).subscribe({
      next: (res) => {
        this.submitting.set(false);
        this.closeCreateModal();

        const sup = this.searchService.getSupplierById(this.createForm.supplierBusinessId);
        const supName = sup?.name || 'Supplier';

        this.showToast(`Sovereign verification request dispatched to ${supName}.`, 'success');
        this.loadRequests();
      },
      error: (err) => {
        this.submitting.set(false);
        console.error('Error creating verification request', err);
        this.showToast('Failed to submit verification request.', 'error');
      }
    });
  }

  // Cancel Request Action (Calls Backend API)
  cancelRequest(requestId: string): void {
    if (!confirm('Are you sure you want to cancel this pending verification request?')) return;

    this.http.post<{ success: boolean }>(`${this.apiUrl}/verification-requests/${requestId}/cancel`, {}).subscribe({
      next: () => {
        this.requests.update(all => all.map(r => r.id === requestId ? { ...r, status: 'CANCELLED' as const } : r));
        if (this.selectedDetailRequest()?.id === requestId) {
          this.selectedDetailRequest.update(d => d ? { ...d, status: 'CANCELLED' as const } : null);
        }
        this.showToast('Verification request marked as cancelled.', 'info');
      },
      error: (err) => {
        console.error('Failed to cancel request on backend', err);
        // Optimistic local update
        this.requests.update(all => all.map(r => r.id === requestId ? { ...r, status: 'CANCELLED' as const } : r));
        if (this.selectedDetailRequest()?.id === requestId) {
          this.selectedDetailRequest.update(d => d ? { ...d, status: 'CANCELLED' as const } : null);
        }
        this.showToast('Verification request cancelled locally.', 'info');
      }
    });
  }

  // Copy Identifier Helper
  copyValue(val: string, label: string): void {
    navigator.clipboard.writeText(val).then(() => {
      this.copiedValue.set(val);
      this.showToast(`${label} copied to clipboard`, 'info');
      setTimeout(() => {
        if (this.copiedValue() === val) {
          this.copiedValue.set(null);
        }
      }, 2000);
    });
  }

  // Toast Notification
  showToast(msg: string, type: 'success' | 'info' | 'error' = 'success'): void {
    this.toastMessage.set(msg);
    this.toastType.set(type);
    setTimeout(() => this.toastMessage.set(null), 3500);
  }

  // Export Ledger CSV
  exportLedgerCsv(): void {
    const list = this.filteredRequests();
    if (list.length === 0) {
      this.showToast('No requests available to export.', 'info');
      return;
    }

    const headers = ['Request ID', 'Target Supplier', 'Trade Name', 'GSTIN', 'City', 'Trust Score', 'Request Type', 'Requested Artifacts', 'Operational Reason', 'Status', 'Date Dispatched', 'Response Summary'];
    const rows = list.map(r => [
      `"${r.id}"`,
      `"${(r.supplierBusinessName || '').replace(/"/g, '""')}"`,
      `"${(r.supplierTradeName || '').replace(/"/g, '""')}"`,
      `"${r.supplierGstin || ''}"`,
      `"${r.supplierCity || ''}"`,
      `"${r.supplierTrustScore || 'N/A'}"`,
      `"${r.requestType}"`,
      `"${(r.requestedItems || []).join('; ')}"`,
      `"${(r.reason || '').replace(/"/g, '""')}"`,
      `"${r.status}"`,
      `"${new Date(r.createdAt).toLocaleDateString()}"`,
      `"${(r.supplierResponse || 'Pending Supplier Fulfillment').replace(/"/g, '""')}"`
    ]);

    const csvContent = 'data:text/csv;charset=utf-8,' + [headers.join(','), ...rows.map(e => e.join(','))].join('\n');
    const encodedUri = encodeURI(csvContent);
    const link = document.createElement('a');
    link.setAttribute('href', encodedUri);
    link.setAttribute('download', `VeriPass_Verification_Requests_${new Date().toISOString().slice(0, 10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    this.showToast('Verification audit ledger exported to CSV.', 'success');
  }

  // Format Helper for Type Labels
  formatType(type?: string): string {
    if (!type) return 'Standard Verification';
    switch (type) {
      case 'VENDOR_ONBOARDING': return 'Vendor Onboarding';
      case 'CREDENTIAL_UPDATE': return 'Credential Renewal';
      case 'AUDIT': return 'Financial & Tender Audit';
      case 'GEO_AUDIT': return 'Plant Geo-Inspection';
      case 'RE_VERIFY': return 'Insurance & Fleet Re-check';
      default: return (type || '').replace(/_/g, ' ');
    }
  }

  // Format Artifact Item Labels
  formatArtifact(code?: string): string {
    if (!code) return 'Document';
    switch (code) {
      case 'BANK_ACCOUNT': return 'Penny-Drop Bank (NPCI)';
      case 'GST': return 'GSTN Filing History';
      case 'ISO_9001': return 'ISO 9001:2015';
      case 'MCA_FINANCIALS': return '3-Yr Financials (MCA)';
      case 'GEO_LOCATION': return 'Plant Geo-Inspection';
      case 'INSURANCE': return 'Transit Insurance';
      case 'UDYAM': return 'MSME Udyam';
      case 'PAN': return 'CBDT PAN Match';
      case 'CIN': return 'MCA21 CIN Standing';
      case 'FACTORY_LICENSE': return 'Factory Premises License';
      case 'SAFETY_AUDIT': return 'Safety Protocol Clearance';
      default: return (code || '').replace(/_/g, ' ');
    }
  }

  // Short ID display: distinctive VR-XXXX identifier
  shortId(id: string): string {
    if (!id) return '';
    if (id.startsWith('REQ-')) return id.replace('REQ-', 'VR-');
    const parts = id.split('-');
    if (parts.length === 5) {
      const last = parts[4];
      return 'VR-' + last.slice(-4).toUpperCase();
    }
    return 'VR-' + id.slice(-6).toUpperCase();
  }
}
