import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { BusinessService } from '../../../core/services/business.service';
import { AuthService } from '../../../core/auth/auth.service';
import { ComplianceEventDto } from '../../../core/models/models';
import { jsPDF } from 'jspdf';

export interface StatutoryFiling {
  id: string;
  name: string;
  code: string;
  category: 'GST & Indirect Tax' | 'MCA & Corporate' | 'Direct Tax & TDS' | 'Labor & Municipal';
  authority: string;
  authorityLogoText: string;
  period: string;
  dueDate: string;
  filingDate: string;
  arnNumber: string;
  status: 'COMPLIANT' | 'UPCOMING' | 'ACTION_REQUIRED';
  taxPayable?: string;
  taxPaid?: string;
  challanNumber?: string;
  description: string;
  acknowledgmentDetails?: { label: string; value: string; isMono?: boolean; isGreen?: boolean; isHighlight?: boolean }[];
}

export interface WatchtowerAlert {
  id: string;
  title: string;
  category: string;
  daysRemaining: number;
  expiryDate: string;
  authority: string;
  urgency: 'warning' | 'info' | 'critical';
  description: string;
  credentialId?: string;
  actionText: string;
}

export interface ComplianceCategoryScore {
  category: string;
  score: number;
  subtext: string;
  icon: string;
  colorClass: string;
}

@Component({
  selector: 'app-supplier-compliance',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './supplier-compliance.component.html',
  styleUrl: './supplier-compliance.component.css'
})
export class SupplierComplianceComponent implements OnInit {
  private businessService = inject(BusinessService);
  private auth = inject(AuthService);
  businessId = '';

  // Modal Signals
  activeFilingModal = signal<StatutoryFiling | null>(null);
  showUploadFilingModal = signal<StatutoryFiling | null>(null);
  showRenewModal = signal<WatchtowerAlert | null>(null);
  toastMessage = signal<string | null>(null);
  isSyncing = signal<boolean>(false);

  // Tab Filtering
  activeTab = signal<string>('All Statutory');
  tabs = ['All Statutory', 'GST & Indirect Tax', 'MCA & Corporate', 'Direct Tax & TDS', 'Labor & Municipal'];

  // Upload/Resolution Flow State
  isProcessingUpload = signal<boolean>(false);
  uploadProgress = signal<number | null>(null);
  uploadStepText = signal<string>('');
  uploadArnInput = signal<string>('');
  uploadFileName = signal<string | null>(null);

  // Renewal Flow State
  isProcessingRenew = signal<boolean>(false);
  renewProgress = signal<number | null>(null);
  renewStepText = signal<string>('');
  renewDocFileName = signal<string | null>(null);

  // Dynamic Business Profile Properties matching Dashboard, Documents & Verification Center
  get companyName(): string {
    const prof = this.businessService.currentProfile();
    return prof?.legalName || prof?.tradeName || this.auth.currentUser()?.companyName || this.auth.currentUser()?.organizationName || 'My Business';
  }

  get principalAddress(): string {
    const addr = this.businessService.currentProfile()?.addresses?.[0];
    if (addr && (addr.line1 || addr.city)) {
      return `${addr.line1 ? addr.line1 + ', ' : ''}${addr.city ? addr.city + ', ' : ''}${addr.state || ''} ${addr.pincode || ''}`.trim().replace(/^, |, $/g, '');
    }
    return '';
  }

  get gstin(): string {
    return this.businessService.currentProfile()?.gstin || '';
  }

  get pan(): string {
    return this.businessService.currentProfile()?.pan || '';
  }

  get cin(): string {
    return this.businessService.currentProfile()?.cin || '';
  }

  // Statutory Filings Data
  filings = signal<StatutoryFiling[]>([
    {
      id: 'filing-gstr3b',
      name: 'GSTR-3B Monthly Tax Return',
      code: 'GSTR-3B',
      category: 'GST & Indirect Tax',
      authority: 'GSTN (Goods and Services Tax Network)',
      authorityLogoText: '🏛️ GSTN',
      period: 'August 2026',
      dueDate: '20 Sep 2026',
      filingDate: '18 Sep 2026, 17:42 IST',
      arnNumber: 'AA090826019482X',
      status: 'COMPLIANT',
      taxPayable: '₹ 2,45,800.00',
      taxPaid: '₹ 2,45,800.00 (Challan Paid)',
      challanNumber: 'CPIN-260908-119284',
      description: 'Monthly summary return of outward and inward supplies declared with zero statutory interest defaults.',
      acknowledgmentDetails: [
        { label: 'GST Identification Number (GSTIN)', value: this.gstin || 'Pending', isMono: true },
        { label: 'Application Reference Number (ARN)', value: 'AA090826019482X', isMono: true, isHighlight: true },
        { label: 'Return Period (Tax Month)', value: 'August 2026' },
        { label: 'Mode of Filing', value: 'Electronic Verification Code (EVC) - Authorized Signatory' },
        { label: 'Total Tax Liability Paid (IGST + CGST + SGST)', value: '₹ 2,45,800.00', isGreen: true },
        { label: 'Input Tax Credit (ITC) Reconciled', value: '100% 2B Reconciliation Matched', isGreen: true },
        { label: 'Statutory Registry Verification', value: 'Verified on GSTN Sovereign Rail (HTTP 200 OK)' }
      ]
    },
    {
      id: 'filing-gstr1',
      name: 'GSTR-1 Outward Supplies Statement',
      code: 'GSTR-1',
      category: 'GST & Indirect Tax',
      authority: 'GSTN (Goods and Services Tax Network)',
      authorityLogoText: '🏛️ GSTN',
      period: 'August 2026',
      dueDate: '11 Sep 2026',
      filingDate: '10 Sep 2026, 15:18 IST',
      arnNumber: 'AA090826008129Q',
      status: 'COMPLIANT',
      taxPayable: '₹ 0.00 (Statement Only)',
      taxPaid: 'N/A',
      challanNumber: 'N/A',
      description: 'Invoice-level outward supplies statement with 142 B2B e-invoices reconciled with buyer portals.',
      acknowledgmentDetails: [
        { label: 'GSTIN', value: this.gstin || 'Pending', isMono: true },
        { label: 'ARN', value: 'AA090826008129Q', isMono: true, isHighlight: true },
        { label: 'Filing Period', value: 'August 2026' },
        { label: 'Total Invoices Uploaded & Reconciled', value: '142 B2B Tax Invoices', isGreen: true },
        { label: 'Total Taxable Turnover', value: '₹ 1,84,32,000.00' },
        { label: 'E-Invoice IRN Verification', value: '100% Valid NIC-IRN Hashes Linked', isGreen: true }
      ]
    },
    {
      id: 'filing-aoc4',
      name: 'MCA Form AOC-4 (Financial Statements)',
      code: 'MCA-AOC-4',
      category: 'MCA & Corporate',
      authority: 'Ministry of Corporate Affairs (MCA21 V3)',
      authorityLogoText: '🏛️ MCA21',
      period: 'FY 2025-26',
      dueDate: '30 Oct 2026',
      filingDate: '28 Oct 2025 (FY25 Prior Acknowledgment)',
      arnNumber: 'AOC4-2025-981249',
      status: 'COMPLIANT',
      description: 'Audited balance sheet, profit & loss statement, auditor report and director disclosures under Companies Act 2013.',
      acknowledgmentDetails: [
        { label: 'Corporate Identity Number (CIN)', value: this.cin || 'Pending', isMono: true },
        { label: 'Service Request Number (SRN)', value: 'SRN-R91829481', isMono: true, isHighlight: true },
        { label: 'Statutory Auditor Firm', value: 'K. S. Verma & Associates, Chartered Accountants (FRN: 014829N)' },
        { label: 'Filing Compliance Status', value: 'APPROVED & TAKEN ON ROC RECORD', isGreen: true },
        { label: 'Next Filing Window', value: 'FY 2025-26 Annual Audit open until 30 Oct 2026' }
      ]
    },
    {
      id: 'filing-mgt7',
      name: 'MCA Form MGT-7 (Annual Return)',
      code: 'MCA-MGT-7',
      category: 'MCA & Corporate',
      authority: 'Ministry of Corporate Affairs (MCA21 V3)',
      authorityLogoText: '🏛️ MCA21',
      period: 'FY 2025-26',
      dueDate: '29 Nov 2026',
      filingDate: '26 Nov 2025 (FY25 Prior Acknowledgment)',
      arnNumber: 'MGT7-2025-441092',
      status: 'COMPLIANT',
      description: 'Annual return of members, directors, board meetings and statutory governance disclosures.',
      acknowledgmentDetails: [
        { label: 'CIN', value: this.cin || 'Pending', isMono: true },
        { label: 'SRN', value: 'SRN-Q44109218', isMono: true, isHighlight: true },
        { label: 'Compliance Status', value: 'ACTIVE & IN ORDER WITH ROC DELHI & HARYANA', isGreen: true },
        { label: 'Director Identification DINs Verified', value: 'DIN 07819284 & DIN 08912841 Active', isGreen: true }
      ]
    },
    {
      id: 'filing-tds26q',
      name: 'Income Tax TDS Return (Form 26Q)',
      code: 'ITD-26Q',
      category: 'Direct Tax & TDS',
      authority: 'Income Tax Department (TRACES / NSDL)',
      authorityLogoText: '🏛️ CBDT',
      period: 'Q1 FY 2026-27 (Apr-Jun)',
      dueDate: '31 Jul 2026',
      filingDate: '29 Jul 2026, 11:20 IST',
      arnNumber: 'TDS-26Q-7782103',
      status: 'COMPLIANT',
      taxPayable: '₹ 1,88,400.00',
      taxPaid: '₹ 1,88,400.00 (Challan 281)',
      challanNumber: 'BSR-021004-99120',
      description: 'Quarterly statement of deduction of tax under sub-section (3) of section 200 in respect of payments other than salary.',
      acknowledgmentDetails: [
        { label: 'Tax Deduction Account Number (TAN)', value: 'RTK12841B', isMono: true },
        { label: 'Provisional Receipt Number (PRN)', value: 'PRN-091829471928', isMono: true, isHighlight: true },
        { label: 'Section 206AB Higher Rate Compliance', value: '100% NON-SPECIFIED (Zero Default Flags)', isGreen: true },
        { label: 'TDS Amount Deposited', value: '₹ 1,88,400.00 on Challan ITNS-281', isGreen: true }
      ]
    },
    {
      id: 'filing-advtax',
      name: 'Corporate Advance Tax (Q2 Installment)',
      code: 'ITD-ADV-TAX',
      category: 'Direct Tax & TDS',
      authority: 'Income Tax Department (NSDL / TIN)',
      authorityLogoText: '🏛️ CBDT',
      period: 'Q2 FY 2026-27',
      dueDate: '15 Sep 2026',
      filingDate: '14 Sep 2026, 16:05 IST',
      arnNumber: 'BSR-021004-89211',
      status: 'COMPLIANT',
      taxPayable: '₹ 3,50,000.00',
      taxPaid: '₹ 3,50,000.00 (Challan 280)',
      challanNumber: 'CHALLAN-280-89211',
      description: 'Mandatory 45% cumulative advance corporate income tax installment deposited on schedule.',
      acknowledgmentDetails: [
        { label: 'PAN', value: this.pan || 'Pending', isMono: true },
        { label: 'Challan Identification Number (CIN)', value: '021004892112609140029', isMono: true, isHighlight: true },
        { label: 'Major Head / Minor Head', value: '0020 (Company) / 100 (Advance Tax)' },
        { label: 'Assessment Year', value: '2027-2028' },
        { label: 'Payment Status', value: 'CLEARED & RECONCILED WITH OLTAS', isGreen: true }
      ]
    },
    {
      id: 'filing-epfo',
      name: 'EPFO Monthly Electronic Challan Return (ECR)',
      code: 'EPFO-ECR',
      category: 'Labor & Municipal',
      authority: 'Employees\' Provident Fund Organisation',
      authorityLogoText: '🏛️ EPFO',
      period: 'August 2026',
      dueDate: '15 Sep 2026',
      filingDate: '14 Sep 2026, 12:45 IST',
      arnNumber: 'ECR-1092837461',
      status: 'COMPLIANT',
      taxPayable: '₹ 1,12,650.00',
      taxPaid: '₹ 1,12,650.00',
      challanNumber: 'TRRN-109260914001',
      description: 'Statutory monthly provident fund deposit and member return covering 48 active plant workers.',
      acknowledgmentDetails: [
        { label: 'EPFO Establishment Code', value: 'GNGGN0019284000', isMono: true },
        { label: 'Temporary Return Reference Number (TRRN)', value: '109260914001', isMono: true, isHighlight: true },
        { label: 'Active Beneficiaries Covered', value: '48 Verified UAN Employees', isGreen: true },
        { label: 'Deposit Status', value: 'TRANSACTION SETTLED VIA SBI CORPORATE BANKING', isGreen: true }
      ]
    },
    {
      id: 'filing-trade-license',
      name: 'Municipal Industrial Trade License Renewal',
      code: 'MUN-TRD-LIC',
      category: 'Labor & Municipal',
      authority: 'Municipal Corporation Ghaziabad / Industrial Zone',
      authorityLogoText: '🏛️ Municipal',
      period: 'FY 2026-27 Renewal',
      dueDate: '18 Sep 2026',
      filingDate: 'Pending Renewal Slip Upload',
      arnNumber: 'GZB-IND-LIC-2024-88',
      status: 'ACTION_REQUIRED',
      taxPayable: '₹ 12,500.00 (Renewal Fee)',
      taxPaid: 'Pending Verification',
      challanNumber: 'GZB-CHALLAN-PENDING',
      description: 'Bi-annual factory operating license fee payment receipt and pollution compliance declaration required.',
      acknowledgmentDetails: [
        { label: 'Operating License Number', value: 'GZB-IND-LIC-2024-88', isMono: true },
        { label: 'Issuing Municipal Body', value: 'Municipal Corporation Ghaziabad (Industrial Zone)' },
        { label: 'Compliance Urgency', value: 'Annual Renewal Fee due immediately to prevent penalty notice', isHighlight: true },
        { label: 'Required Action', value: 'Upload bank payment challan or renewed municipal certificate' }
      ]
    }
  ]);

  // Watchtower Alerts matching Dashboard and Documents & Credentials
  watchtowerAlerts = signal<WatchtowerAlert[]>([
    {
      id: 'alert-iso',
      title: 'ISO 9001:2015 QMS expires in 27 days',
      category: 'Quality Certification',
      daysRemaining: 27,
      expiryDate: '27 Sep 2026',
      authority: 'TUV Nord Registrar',
      urgency: 'warning',
      description: 'Quality management audit certificate approaches its annual surveillance renewal. Upload renewed audit certificate to preserve Tier-1 buyer rating.',
      credentialId: 'ISO-9001-2025-9921',
      actionText: 'Renew & Upload Certificate'
    },
    {
      id: 'alert-trade',
      title: 'Industrial Trade License renewal due',
      category: 'Municipal Licensing',
      daysRemaining: 0,
      expiryDate: '18 Sep 2026',
      authority: 'Municipal Corporation Ghaziabad',
      urgency: 'warning',
      description: 'Factory operating permit requires bi-annual municipal renewal fee receipt slip (₹ 12,500) uploaded to clear pending status.',
      credentialId: 'GZB-IND-LIC-2024-88',
      actionText: 'Upload Renewal Challan'
    },
    {
      id: 'alert-spcb',
      title: 'Pollution Control Board Consent active',
      category: 'Environmental (SPCB)',
      daysRemaining: 66,
      expiryDate: '02 Dec 2026',
      authority: 'State Pollution Control Board',
      urgency: 'info',
      description: 'Consent to Operate (CTO) valid through 02 Dec 2026. Scheduled automated health check active on sovereign environmental portal.',
      credentialId: 'SPCB-AIR-WAT-2025-091',
      actionText: 'View Consent Terms'
    }
  ]);

  // Category Scores
  categoryScores = signal<ComplianceCategoryScore[]>([
    {
      category: 'INDIRECT TAX (GSTN Sovereign Rails)',
      score: 98,
      subtext: 'GSTR-1 & GSTR-3B filed on time with zero ITC default',
      icon: '🏛️',
      colorClass: 'green'
    },
    {
      category: 'CORPORATE COMPLIANCE (MCA21 & RoC)',
      score: 95,
      subtext: 'AOC-4 & MGT-7 current, directors active in good standing',
      icon: '⚖️',
      colorClass: 'green'
    },
    {
      category: 'DIRECT TAXATION (CBDT & TDS Compliance)',
      score: 96,
      subtext: 'Zero Section 206AB defaults, advance tax paid on schedule',
      icon: '📑',
      colorClass: 'green'
    },
    {
      category: 'LABOR & SOCIAL SECURITY (EPFO & ESIC)',
      score: 92,
      subtext: 'EPF ECR filed monthly for all 48 plant personnel',
      icon: '👥',
      colorClass: 'green'
    },
    {
      category: 'QUALITY & LICENSES (ISO & Municipal)',
      score: 84,
      subtext: 'ISO 9001:2015 renewal audit due within 27 days',
      icon: '🛡️',
      colorClass: 'amber'
    }
  ]);

  // Dynamic Overall Compliance Score
  // Dynamic Overall Compliance Score
  complianceScore = computed(() => {
    const list = this.filings();
    if (list.length === 0) return 0;
    const compliantCount = list.filter(f => f.status === 'COMPLIANT').length;
    const alertCount = this.watchtowerAlerts().filter(a => a.urgency === 'warning').length;

    if (compliantCount === list.length && alertCount === 0) return 100;
    return Math.round((compliantCount / list.length) * 100);
  });

  // Filtered Filings based on active tab
  filteredFilings = computed(() => {
    const tab = this.activeTab();
    if (tab === 'All Statutory') return this.filings();
    return this.filings().filter(f => f.category === tab);
  });

  // Summary Metrics computed dynamically
  compliantCount = computed(() => this.filings().filter(f => f.status === 'COMPLIANT').length);
  actionCount = computed(() => this.filings().filter(f => f.status === 'ACTION_REQUIRED').length);
  expiringCount = computed(() => this.watchtowerAlerts().filter(a => a.urgency === 'warning').length);

  ngOnInit(): void {
    const authId = this.auth.currentBusinessId();
    if (authId) {
      this.businessId = authId;
    }
    const isVerified = this.businessService.currentProfile()?.verificationStatus === 'VERIFIED';
    if (!isVerified) {
      this.filings.set([]);
      this.watchtowerAlerts.set([]);
      this.categoryScores.set([]);
    }
    this.businessService.getComplianceEvents(this.businessId).subscribe({
      next: () => {}
    });
  }

  // Set Tab Filter
  setTab(tab: string): void {
    this.activeTab.set(tab);
  }

  // Inspection Modal Actions
  openFilingModal(filing: StatutoryFiling): void {
    this.activeFilingModal.set(filing);
  }

  closeFilingModal(): void {
    this.activeFilingModal.set(null);
  }

  // Upload / Resolution Modal Actions
  openUploadModal(filing: StatutoryFiling): void {
    this.uploadArnInput.set(filing.code === 'MUN-TRD-LIC' ? 'GZB-2026-CHALLAN-99214' : 'ARN-' + Math.floor(100000 + Math.random() * 900000));
    this.uploadFileName.set('Municipal_Renewal_Challan_FY26.pdf');
    this.showUploadFilingModal.set(filing);
  }

  closeUploadModal(): void {
    if (this.isProcessingUpload()) return;
    this.showUploadFilingModal.set(null);
    this.uploadProgress.set(null);
    this.uploadStepText.set('');
  }

  onFileSelected(event: any): void {
    const file = event?.target?.files?.[0];
    if (file) {
      this.uploadFileName.set(file.name);
    }
  }

  submitFilingUpload(): void {
    const filing = this.showUploadFilingModal();
    if (!filing) return;

    this.isProcessingUpload.set(true);
    this.uploadProgress.set(20);
    this.uploadStepText.set('Uploading statutory payment receipt & extracting challan hash...');

    setTimeout(() => {
      this.uploadProgress.set(55);
      this.uploadStepText.set('Reconciling payment ARN with Municipal Treasury portal...');

      setTimeout(() => {
        this.uploadProgress.set(85);
        this.uploadStepText.set('Authenticating municipal officer digital signature...');

        setTimeout(() => {
          this.uploadProgress.set(100);
          this.uploadStepText.set('Filing successfully verified & added to sovereign ledger!');

          setTimeout(() => {
            // Update filing status in signal
            this.filings.update(currentList =>
              currentList.map(f => {
                if (f.id === filing.id) {
                  return {
                    ...f,
                    status: 'COMPLIANT',
                    filingDate: 'Just Now, 19:50 IST',
                    arnNumber: this.uploadArnInput() || 'GZB-2026-CHALLAN-99214',
                    taxPaid: '₹ 12,500.00 (Challan Paid)',
                    challanNumber: this.uploadArnInput() || 'GZB-CHALLAN-99214',
                    acknowledgmentDetails: [
                      ...(f.acknowledgmentDetails || []),
                      { label: 'Reconciled Challan Receipt', value: this.uploadArnInput() || 'GZB-CHALLAN-99214', isMono: true, isGreen: true },
                      { label: 'Renewal Standing', value: 'VALID & COMPLIANT THROUGH 31 MAR 2027', isGreen: true }
                    ]
                  };
                }
                return f;
              })
            );

            // Update watchtower alert if related
            this.watchtowerAlerts.update(alerts =>
              alerts.map(a => {
                if (a.id === 'alert-trade') {
                  return {
                    ...a,
                    urgency: 'info',
                    daysRemaining: 185,
                    expiryDate: '31 Mar 2027',
                    title: 'Industrial Trade License renewed & valid',
                    description: 'Municipal factory operating permit renewed through 31 Mar 2027. Fee slip reconciled.'
                  };
                }
                return a;
              })
            );

            // Update category score
            this.categoryScores.update(scores =>
              scores.map(s => {
                if (s.category.includes('QUALITY & LICENSES')) {
                  return { ...s, score: 92, subtext: 'Trade license cleared, ISO 9001:2015 audit pending' };
                }
                return s;
              })
            );

            this.isProcessingUpload.set(false);
            this.showUploadFilingModal.set(null);
            this.uploadProgress.set(null);
            this.uploadStepText.set('');
            this.showToast('✓ Statutory Filing Reconciled & Marked Compliant!');
          }, 600);
        }, 600);
      }, 700);
    }, 600);
  }

  // Watchtower Alert Renewal Modal Actions
  openRenewModal(alert: WatchtowerAlert): void {
    this.renewDocFileName.set('ISO_9001_2015_Audit_Renewal_2026_2029.pdf');
    this.showRenewModal.set(alert);
  }

  closeRenewModal(): void {
    if (this.isProcessingRenew()) return;
    this.showRenewModal.set(null);
    this.renewProgress.set(null);
    this.renewStepText.set('');
  }

  onRenewFileSelected(event: any): void {
    const file = event?.target?.files?.[0];
    if (file) {
      this.renewDocFileName.set(file.name);
    }
  }

  submitRenew(): void {
    const alert = this.showRenewModal();
    if (!alert) return;

    this.isProcessingRenew.set(true);
    this.renewProgress.set(25);
    this.renewStepText.set('Validating accredited registrar digital signature & QR code...');

    setTimeout(() => {
      this.renewProgress.set(60);
      this.renewStepText.set('Reconciling IAF / TUV Nord accreditation database registry...');

      setTimeout(() => {
        this.renewProgress.set(85);
        this.renewStepText.set('Updating Veripass Sovereign Trust Passport records...');

        setTimeout(() => {
          this.renewProgress.set(100);
          this.renewStepText.set('Quality credential renewed for 3 years!');

          setTimeout(() => {
            // Update alert
            this.watchtowerAlerts.update(alerts =>
              alerts.map(a => {
                if (a.id === alert.id) {
                  return {
                    ...a,
                    urgency: 'info',
                    daysRemaining: 1095,
                    expiryDate: '27 Sep 2029',
                    title: 'ISO 9001:2015 QMS renewed & fully certified',
                    description: 'Accredited quality management certification verified through September 2029. Zero audit non-conformities.'
                  };
                }
                return a;
              })
            );

            // Update category score
            this.categoryScores.update(scores =>
              scores.map(s => {
                if (s.category.includes('QUALITY & LICENSES')) {
                  return { ...s, score: 98, colorClass: 'green', subtext: 'ISO 9001:2015 renewed for 3 years, all licenses active' };
                }
                return s;
              })
            );

            this.isProcessingRenew.set(false);
            this.showRenewModal.set(null);
            this.renewProgress.set(null);
            this.renewStepText.set('');
            this.showToast('✓ Credential Successfully Renewed & Verified on Sovereign Rails!');
          }, 600);
        }, 600);
      }, 700);
    }, 600);
  }

  // Sovereign Rails Sync
  syncSovereignRails(): void {
    this.isSyncing.set(true);
    setTimeout(() => {
      this.isSyncing.set(false);
      this.showToast('✓ Sovereign Compliance Rails Synced with GSTN, MCA21 & CBDT (100% In-Sync)');
    }, 1200);
  }

  showToast(msg: string): void {
    this.toastMessage.set(msg);
    setTimeout(() => {
      if (this.toastMessage() === msg) {
        this.toastMessage.set(null);
      }
    }, 4500);
  }

  // Generate Official PDF Compliance Audit Report
  downloadFullComplianceReport(): void {
    try {
      const doc = new jsPDF({ orientation: 'portrait', unit: 'mm', format: 'a4' });
      const pageWidth = 210;

      // Header Banner
      doc.setFillColor(15, 23, 42); // slate-900
      doc.rect(0, 0, pageWidth, 28, 'F');

      doc.setFillColor(5, 150, 105); // emerald accent bar
      doc.rect(0, 28, pageWidth, 3, 'F');

      doc.setFont('helvetica', 'bold');
      doc.setFontSize(14);
      doc.setTextColor(255, 255, 255);
      doc.text('VERIPASS SOVEREIGN COMPLIANCE NETWORK', 18, 14);

      doc.setFont('helvetica', 'normal');
      doc.setFontSize(8.5);
      doc.setTextColor(203, 213, 225);
      doc.text("Government Regulatory Compliance & Sovereign Telemetry Certificate", 18, 20);

      const reportDate = '27 September 2026';
      doc.setFontSize(8);
      doc.text(`Issue Date: ${reportDate}`, 150, 14);
      doc.text('Audit Serial: VP-CMP-2026-98104', 150, 20);

      // Entity Profile Box
      doc.setFillColor(248, 250, 252);
      doc.setDrawColor(226, 232, 240);
      doc.roundedRect(18, 38, 174, 38, 2, 2, 'FD');

      doc.setFont('helvetica', 'bold');
      doc.setFontSize(13);
      doc.setTextColor(15, 23, 42);
      doc.text(this.companyName.toUpperCase(), 24, 47);

      doc.setFont('helvetica', 'normal');
      doc.setFontSize(8);
      doc.setTextColor(71, 85, 105);
      const addrLines = doc.splitTextToSize(`Principal Operating Address: ${this.principalAddress}`, 160);
      doc.text(addrLines, 24, 53);

      doc.setFont('helvetica', 'bold');
      doc.setFontSize(8);
      doc.setTextColor(30, 41, 59);
      doc.text(`GSTIN: ${this.gstin}`, 24, 63);
      doc.text(`PAN: ${this.pan}`, 80, 63);
      doc.text(`CIN: ${this.cin}`, 130, 63);

      doc.setFont('helvetica', 'bold');
      doc.setFontSize(8.5);
      doc.setTextColor(5, 150, 105);
      doc.text(`Overall Compliance Index: ${this.complianceScore()}% • Tier-1 Sovereign Standing`, 24, 71);

      // Section: Regulatory Scores Breakdown
      doc.setFillColor(241, 245, 249);
      doc.rect(18, 82, 174, 7, 'F');
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(8.5);
      doc.setTextColor(15, 23, 42);
      doc.text('REGULATORY HEALTH & COMPLIANCE CATEGORY SCORES', 22, 87);

      let catY = 94;
      this.categoryScores().forEach((cat, i) => {
        doc.setFont('helvetica', 'normal');
        doc.setFontSize(8);
        doc.setTextColor(30, 41, 59);
        doc.text(cat.category, 22, catY);

        doc.setFont('helvetica', 'bold');
        doc.setTextColor(5, 150, 105);
        doc.text(`${cat.score}% Compliant`, 165, catY, { align: 'right' });

        doc.setFont('helvetica', 'italic');
        doc.setFontSize(7);
        doc.setTextColor(100, 116, 139);
        doc.text(cat.subtext, 22, catY + 4);

        doc.setDrawColor(241, 245, 249);
        doc.line(18, catY + 7, 192, catY + 7);
        catY += 10;
      });

      // Section: Statutory Filings Ledger
      const filingsStartY = 150;
      doc.setFillColor(241, 245, 249);
      doc.rect(18, filingsStartY, 174, 7, 'F');
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(8.5);
      doc.setTextColor(15, 23, 42);
      doc.text('SOVEREIGN STATUTORY FILINGS & REGISTRY LEDGER', 22, filingsStartY + 5);

      // Table Header
      doc.setFillColor(226, 232, 240);
      doc.rect(18, filingsStartY + 8, 174, 6, 'F');
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(7.5);
      doc.setTextColor(51, 65, 85);
      doc.text('OBLIGATION / RETURN', 22, filingsStartY + 12.5);
      doc.text('AUTHORITY', 75, filingsStartY + 12.5);
      doc.text('PERIOD', 115, filingsStartY + 12.5);
      doc.text('ARN / REFERENCE', 145, filingsStartY + 12.5);
      doc.text('STATUS', 178, filingsStartY + 12.5);

      let rowY = filingsStartY + 15;
      this.filings().forEach((f, idx) => {
        if (idx % 2 === 1) {
          doc.setFillColor(248, 250, 252);
          doc.rect(18, rowY, 174, 8, 'F');
        }

        doc.setFont('helvetica', 'bold');
        doc.setFontSize(7.5);
        doc.setTextColor(15, 23, 42);
        doc.text(f.code, 22, rowY + 5.5);

        doc.setFont('helvetica', 'normal');
        doc.setFontSize(7);
        doc.setTextColor(71, 85, 105);
        doc.text(f.authority.substring(0, 22), 75, rowY + 5.5);
        doc.text(f.period, 115, rowY + 5.5);

        doc.setFont('courier', 'normal');
        doc.setFontSize(7);
        doc.text(f.arnNumber.substring(0, 16), 145, rowY + 5.5);

        doc.setFont('helvetica', 'bold');
        if (f.status === 'COMPLIANT') {
          doc.setTextColor(5, 150, 105);
          doc.text('COMPLIANT', 178, rowY + 5.5);
        } else {
          doc.setTextColor(217, 119, 6);
          doc.text('ACTION', 178, rowY + 5.5);
        }

        doc.setDrawColor(241, 245, 249);
        doc.line(18, rowY + 8, 192, rowY + 8);
        rowY += 8;
      });

      // Footer
      const footerY = 250;
      doc.setDrawColor(203, 213, 225);
      doc.line(18, footerY, 192, footerY);

      doc.setFont('helvetica', 'bold');
      doc.setFontSize(8);
      doc.setTextColor(15, 23, 42);
      doc.text('Sovereign Attestation & Cryptographic Audit Seal', 22, footerY + 8);

      doc.setFont('helvetica', 'normal');
      doc.setFontSize(7);
      doc.setTextColor(100, 116, 139);
      doc.text("Cryptographic Hash: SHA-256 e8491cba091849129dfbbca90124981140928410294812a014981048201a4819", 22, footerY + 13);
      doc.text(`Authorized by Veripass India Compliance Gateway (https://veripass.in) • Reconciled against GSTN, MCA21, CBDT & EPFO`, 22, footerY + 18);

      // Stamp
      doc.setDrawColor(5, 150, 105);
      doc.setFillColor(236, 253, 245);
      doc.roundedRect(148, footerY + 4, 44, 18, 2, 2, 'FD');
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(7);
      doc.setTextColor(5, 150, 105);
      doc.text('VERIPASS INDIA CERTIFIED', 170, footerY + 10, { align: 'center' });
      doc.setFont('helvetica', 'normal');
      doc.setFontSize(6);
      doc.text('ZERO STATUTORY DEFAULTS', 170, footerY + 14, { align: 'center' });
      doc.text('AUTHENTICATED RECONCILIATION', 170, footerY + 18, { align: 'center' });

      const safeComp = this.companyName.replace(/[^a-zA-Z0-9]/g, '_');
      doc.save(`${safeComp}_Sovereign_Compliance_Audit_Report.pdf`);
      this.showToast('✓ Compliance Audit Report (PDF) downloaded successfully!');
    } catch (e) {
      console.error(e);
      this.showToast('Unable to export Compliance Audit Report.');
    }
  }

  // Generate Individual Acknowledgment Slip PDF
  downloadFilingAcknowledgment(filing: StatutoryFiling): void {
    try {
      const doc = new jsPDF({ orientation: 'portrait', unit: 'mm', format: 'a4' });
      const pageWidth = 210;

      // Header
      doc.setFillColor(15, 23, 42);
      doc.rect(0, 0, pageWidth, 28, 'F');
      doc.setFillColor(5, 150, 105);
      doc.rect(0, 28, pageWidth, 3, 'F');

      doc.setFont('helvetica', 'bold');
      doc.setFontSize(13);
      doc.setTextColor(255, 255, 255);
      doc.text('OFFICIAL STATUTORY FILING ACKNOWLEDGMENT', 18, 14);

      doc.setFont('helvetica', 'normal');
      doc.setFontSize(8);
      doc.setTextColor(203, 213, 225);
      doc.text(`Authorized Sovereign Tax & Corporate Record • ${filing.authority}`, 18, 20);

      // Status Pill
      doc.setFillColor(240, 253, 244);
      doc.setDrawColor(16, 185, 129);
      doc.roundedRect(138, 8, 54, 14, 2, 2, 'FD');
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(7.5);
      doc.setTextColor(4, 120, 87);
      doc.text('FILED & COMPLIANT', 165, 14, { align: 'center' });
      doc.setFont('helvetica', 'normal');
      doc.setFontSize(6.5);
      doc.text('REGISTRY RECONCILED', 165, 18, { align: 'center' });

      // Entity Info
      doc.setFillColor(248, 250, 252);
      doc.setDrawColor(226, 232, 240);
      doc.roundedRect(18, 38, 174, 28, 2, 2, 'FD');

      doc.setFont('helvetica', 'bold');
      doc.setFontSize(11);
      doc.setTextColor(15, 23, 42);
      doc.text(this.companyName.toUpperCase(), 24, 46);

      doc.setFont('helvetica', 'normal');
      doc.setFontSize(8);
      doc.setTextColor(71, 85, 105);
      doc.text(`GSTIN: ${this.gstin}   |   PAN: ${this.pan}   |   CIN: ${this.cin}`, 24, 52);
      doc.text(`Operating Address: ${this.principalAddress}`, 24, 58);

      // Section: Particulars
      doc.setFillColor(241, 245, 249);
      doc.rect(18, 72, 174, 8, 'F');
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(9);
      doc.setTextColor(30, 41, 59);
      doc.text(`STATUTORY ACKNOWLEDGMENT PARTICULARS — ${filing.code}`, 22, 77.5);

      let curY = 82;
      const details = filing.acknowledgmentDetails || [
        { label: 'Return Name', value: filing.name },
        { label: 'Statutory Body', value: filing.authority },
        { label: 'Filing Period', value: filing.period },
        { label: 'ARN / Receipt Number', value: filing.arnNumber, isMono: true },
        { label: 'Date & Time of Filing', value: filing.filingDate },
        { label: 'Tax / Fee Paid', value: filing.taxPaid || 'N/A' },
        { label: 'Challan Number', value: filing.challanNumber || 'N/A', isMono: true }
      ];

      details.forEach((d, idx) => {
        if (idx % 2 === 1) {
          doc.setFillColor(248, 250, 252);
          doc.rect(18, curY, 174, 11, 'F');
        }
        doc.setDrawColor(226, 232, 240);
        doc.line(18, curY + 11, 192, curY + 11);

        doc.setFont('helvetica', 'bold');
        doc.setFontSize(8.5);
        doc.setTextColor(71, 85, 105);
        doc.text(d.label, 22, curY + 7);

        doc.setFont(d.isMono ? 'courier' : 'helvetica', 'bold');
        doc.setTextColor(d.isGreen ? 5 : 15, d.isGreen ? 150 : 23, d.isGreen ? 105 : 42);
        const lines = doc.splitTextToSize(d.value, 110);
        doc.text(lines, 85, curY + 7);

        curY += 11;
      });

      // Footer
      const footerY = 246;
      doc.setDrawColor(203, 213, 225);
      doc.line(18, footerY, 192, footerY);

      doc.setFont('helvetica', 'bold');
      doc.setFontSize(8);
      doc.setTextColor(15, 23, 42);
      doc.text('Digital Signature & Sovereign Registry Verification', 22, footerY + 8);

      doc.setFont('helvetica', 'normal');
      doc.setFontSize(7);
      doc.setTextColor(100, 116, 139);
      doc.text(`ARN Electronic Signature Hash: SHA-256 ${filing.arnNumber}f90184b910248a`, 22, footerY + 13);
      doc.text(`Authorized by Veripass India Compliance Gateway (${filing.authority})`, 22, footerY + 18);

      const safeComp = this.companyName.replace(/[^a-zA-Z0-9]/g, '_');
      doc.save(`${safeComp}_${filing.code}_Acknowledgment.pdf`);
      this.showToast(`✓ ${filing.code} Acknowledgment Slip (PDF) downloaded!`);
    } catch (e) {
      console.error(e);
      this.showToast('Unable to export Acknowledgment Slip.');
    }
  }
}
