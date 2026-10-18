import { Injectable, signal, computed, inject } from '@angular/core';
import { ApiService } from './api.service';

export interface AdminBusiness {
  passportId: string;
  businessName: string;
  legalName: string;
  gstin: string;
  cin: string;
  verifyScore: number;
  riskRating: 'LOW' | 'MEDIUM' | 'HIGH';
  status: 'ACTIVE' | 'FLAGGED' | 'SUSPENDED';
  industry: string;
  state: string;
  registeredDate: string;
  credentialsCount: number;
  flagReason?: string;
  suspensionReason?: string;
}

export interface AdminBuyer {
  orgId: string;
  organizationName: string;
  industry: string;
  usersCount: number;
  suppliersCount: number;
  verificationRequestsCount: number;
  status: 'ACTIVE' | 'PENDING' | 'SUSPENDED';
  joinedDate: string;
  suspensionReason?: string;
}

export interface VerificationRule {
  id: string;
  name: string;
  type: 'Identity' | 'Compliance' | 'Risk';
  version: string;
  status: 'ACTIVE' | 'DRAFT';
  appliesTo: string;
  whenField: string;
  whenOperator: string;
  whenValue: string;
  andField?: string;
  andOperator?: string;
  andValue?: string;
  thenAction: string;
  failureAction: string;
  priority: 'HIGH' | 'MEDIUM' | 'LOW';
  createdBy: string;
  createdAt: string;
  effectiveFrom: string;
  changeReason?: string;
}

export interface CredentialTypeConfig {
  code: string;
  displayName: string;
  category: 'Tax' | 'Identity' | 'Corporate' | 'Compliance' | 'Banking';
  status: 'ACTIVE' | 'INACTIVE';
  verificationMethod: 'API' | 'Document' | 'Manual';
  hasExpiry: boolean;
  required: boolean;
  sources: { api: boolean; document: boolean; manual: boolean };
  reverificationPeriod: string;
}

export interface AdminDocument {
  id: string;
  fileName: string;
  businessName: string;
  passportId: string;
  credentialType: string;
  fileType: string;
  uploadedAt: string;
  status: 'VERIFIED' | 'PROCESSING' | 'QUARANTINE' | 'EXPIRED' | 'REJECTED';
  risk: 'LOW' | 'MEDIUM' | 'HIGH';
  sha256: string;
  malwareScanResult: string;
  quarantineReason?: string;
}

export interface AdminDispute {
  id: string;
  businessName: string;
  passportId: string;
  originalCaseId: string;
  credentialType: string;
  reason: string;
  supplierStatement: string;
  originalDecision: string;
  previousVerifier: string;
  status: 'OPEN' | 'REVIEW' | 'RESOLVED' | 'ESCALATED' | 'REJECTED';
  openedAt: string;
  evidenceExamined: string[];
  resolutionAction?: 'UPHOLD' | 'REVERIFY' | 'ESCALATE';
  resolutionReason?: string;
  resolvedAt?: string;
}

export interface AdminAuditLog {
  id: string;
  time: string;
  actor: string;
  role: string;
  action: string;
  resource: string;
  result: 'SUCCESS' | 'FAILURE' | 'WARNING';
  ip: string;
  correlationId: string;
  reason?: string;
  previousState?: string;
  newState?: string;
}

export interface AdminUser {
  userId: string;
  name: string;
  email: string;
  role: 'SUPPLIER' | 'BUYER_ADMIN' | 'BUYER_USER' | 'VERIFIER' | 'SENIOR_VERIFIER' | 'COMPLIANCE_ADMIN' | 'PLATFORM_ADMIN';
  status: 'ACTIVE' | 'SUSPENDED' | 'PENDING';
  lastLogin: string;
}

export interface AdminSession {
  id: string;
  user: string;
  device: string;
  location: string;
  lastActive: string;
  ip: string;
  isCurrent: boolean;
}

export interface SystemConfiguration {
  general: {
    platformName: string;
    supportEmail: string;
    maintenanceMode: boolean;
    allowPublicRegistrations: boolean;
    defaultTimezone: string;
  };
  authentication: {
    enforceMfaAllAdmins: boolean;
    allowSmsMfaForStaff: boolean;
    sessionInactivityTimeoutMinutes: number;
    maxConcurrentSessionsPerUser: number;
    passwordExpiryDays: number;
  };
  verification: {
    requireHumanDecision: boolean; // LOCKED TRUE
    minOcrConfidencePercent: number;
    minScoreForAutoPrecheck: number;
    validityPeriodMonths: number;
    maxDisputeFilingWindowDays: number;
  };
  integrations: {
    id: string;
    name: string;
    endpoint: string;
    status: 'ONLINE' | 'DEGRADED' | 'OFFLINE';
    latencyMs: number;
    lastPing: string;
    protocol: string;
  }[];
}

export interface SecurityEventAlert {
  id: string;
  timestamp: string;
  severity: 'CRITICAL' | 'HIGH' | 'MEDIUM' | 'LOW';
  title: string;
  description: string;
  sourceIp: string;
  affectedAccount: string;
  status: 'INVESTIGATING' | 'RESOLVED' | 'DISMISSED';
}

@Injectable({
  providedIn: 'root'
})
export class AdminStateService {
  private api = inject(ApiService);

  constructor() {
    this.loadBusinesses();
    this.loadDashboardMetrics();
    this.loadBuyers();
    this.loadAuditLogs();
  }

  loadBusinesses(): void {
    this.api.get<any[]>('admin/businesses').subscribe({
      next: (list) => {
        if (Array.isArray(list) && list.length > 0) {
          const mapped: AdminBusiness[] = list.map(b => {
            const rawStatus = (b.verificationStatus || '').toUpperCase();
            let status: 'ACTIVE' | 'FLAGGED' | 'SUSPENDED' = 'ACTIVE';
            if (rawStatus === 'FLAGGED') status = 'FLAGGED';
            else if (rawStatus === 'SUSPENDED' || rawStatus === 'REJECTED') status = 'SUSPENDED';

            const score = b.trustScore ?? 0;
            let risk: 'LOW' | 'MEDIUM' | 'HIGH' = 'LOW';
            if (b.riskRating) {
              const r = b.riskRating.toUpperCase();
              risk = r === 'HIGH' ? 'HIGH' : (r === 'MEDIUM' ? 'MEDIUM' : 'LOW');
            } else {
              risk = score < 50 ? 'HIGH' : (score < 75 ? 'MEDIUM' : 'LOW');
            }

            return {
              passportId: b.passportId || b.businessIdNumber || b.id,
              businessName: b.tradeName || b.legalName || 'Registered Enterprise',
              legalName: b.legalName || 'Registered Enterprise',
              gstin: b.gstin || '—',
              cin: b.cin || '—',
              verifyScore: score,
              riskRating: risk,
              status: status,
              industry: b.industry || 'Precision Manufacturing & Engineering',
              state: b.state || 'Maharashtra',
              registeredDate: b.createdAt ? new Date(b.createdAt).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) : 'Recent',
              credentialsCount: b.credentialsCount ?? 0,
              flagReason: b.flagReason,
              suspensionReason: b.suspensionReason
            };
          });
          this.businessesSignal.set(mapped);
        }
      },
      error: (err) => console.warn('Could not fetch admin businesses, using local default:', err)
    });
  }

  loadDashboardMetrics(): void {
    this.api.get<any>('admin/dashboard').subscribe({
      next: (d) => {
        if (d) {
          this.platformKpis.update(prev => ({
            ...prev,
            totalBusinesses: d.totalBusinesses ?? prev.totalBusinesses,
            verifiedBusinesses: d.totalVerifiedPassports ?? prev.verifiedBusinesses,
            pendingCases: d.pendingVerifications ?? prev.pendingCases,
            highRiskBusinesses: d.activeRiskSignals ?? prev.highRiskBusinesses,
            totalDisputes: d.openDisputes ?? prev.totalDisputes
          }));
        }
      },
      error: (err) => console.warn('Could not fetch admin dashboard, using local default:', err)
    });
  }

  loadBuyers(): void {
    this.api.get<any[]>('admin/buyers').subscribe({
      next: (list) => {
        if (Array.isArray(list) && list.length > 0) {
          const mapped: AdminBuyer[] = list.map(b => ({
            orgId: b.id || b.orgId,
            organizationName: b.name || b.organizationName || 'Procurement Organization',
            industry: b.industry || 'Procurement & Supply Chain',
            usersCount: b.memberCount ?? b.usersCount ?? 1,
            suppliersCount: b.supplierCount ?? b.suppliersCount ?? 0,
            verificationRequestsCount: b.verificationRequestsCount ?? 0,
            status: (b.status?.toUpperCase() as any) || 'ACTIVE',
            joinedDate: b.createdAt ? new Date(b.createdAt).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) : '10 Jan 2026'
          }));
          this.buyersSignal.set(mapped);
        }
      },
      error: (err) => console.warn('Could not fetch admin buyers:', err)
    });
  }

  loadAuditLogs(): void {
    this.api.get<any[]>('admin/audit-logs').subscribe({
      next: (logs) => {
        if (Array.isArray(logs) && logs.length > 0) {
          const mapped: AdminAuditLog[] = logs.map(a => ({
            id: a.id ? (typeof a.id === 'string' ? a.id : a.id.toString()) : 'AUD-' + Math.floor(Math.random() * 100000),
            time: a.createdAt ? new Date(a.createdAt).toLocaleString('en-GB') : 'Just now',
            actor: a.userEmail || a.userId || 'admin@veripass.in',
            role: 'PLATFORM_ADMIN',
            action: a.action || 'ACTIVITY_LOGGED',
            resource: a.resourceType || a.resourceId || 'System',
            result: (a.result?.toUpperCase() as any) || 'SUCCESS',
            ip: a.ipAddress || '127.0.0.1',
            correlationId: 'REQ-' + (a.id ? a.id.toString().substring(0, 8) : '001'),
            reason: a.description || 'System action executed'
          }));
          this.auditLogsSignal.set(mapped);
        }
      },
      error: (err) => console.warn('Could not fetch admin audit logs:', err)
    });
  }

  // Platform Metrics Signals
  platformKpis = signal({
    totalBusinesses: 84291,
    businessesGrowth: '+4.2%',
    verifiedBusinesses: 62841,
    verifiedRate: '74.6%',
    totalBuyers: 8912,
    buyersGrowth: '+2.1%',
    pendingCases: 4210,
    highRiskBusinesses: 421,
    highRiskChange: '↑ 8.2%',
    totalDisputes: 84,
    unresolvedDisputes: 12
  });

  platformHealth = signal([
    { service: 'Verification API Gateway', status: 'Operational', latency: '42ms', uptime: '99.98%' },
    { service: 'OCR Inference Engine', status: 'Operational', latency: '180ms', uptime: '99.94%' },
    { service: 'Sovereign Gateways (GSTN/MCA/CBDT)', status: 'Operational', latency: '240ms', uptime: '99.85%' },
    { service: 'Document Storage & Vault', status: 'Operational', latency: '35ms', uptime: '100.00%' },
    { service: 'Notification Dispatcher', status: 'Operational', latency: '85ms', uptime: '99.99%' },
    { service: 'Risk & Anomaly Engine', status: 'Operational', latency: '110ms', uptime: '99.95%' }
  ]);

  // A2: Businesses Registry (Real registered suppliers)
  private businessesSignal = signal<AdminBusiness[]>([
    {
      passportId: 'VP-IN-2026-92644',
      businessName: 'ABC INDUSTRIAL DYNAMICS',
      legalName: 'ABC INDUSTRIAL DYNAMICS',
      gstin: '27AABCU9603R1ZM',
      cin: 'U28999MH2020PTC345678',
      verifyScore: 88,
      riskRating: 'LOW',
      status: 'ACTIVE',
      industry: 'Precision Engineering & Tooling',
      state: 'Maharashtra',
      registeredDate: '07 Oct 2026',
      credentialsCount: 4
    },
    {
      passportId: 'VP-IN-2026-46190',
      businessName: 'Axionis manufacturing pvt ltd',
      legalName: 'Axionis manufacturing pvt ltd',
      gstin: '29AABCA1234F1Z5',
      cin: 'U72900KA2021PTC145678',
      verifyScore: 82,
      riskRating: 'LOW',
      status: 'ACTIVE',
      industry: 'IT & Electronic Systems',
      state: 'Karnataka',
      registeredDate: '06 Oct 2026',
      credentialsCount: 3
    }
  ]);

  // A3: Buyer Organizations
  private buyersSignal = signal<AdminBuyer[]>([
    {
      orgId: 'ORG-10291',
      organizationName: 'XYZ Corporation Procurement Global',
      industry: 'Automotive OEM',
      usersCount: 42,
      suppliersCount: 821,
      verificationRequestsCount: 1284,
      status: 'ACTIVE',
      joinedDate: '10 Jan 2026'
    },
    {
      orgId: 'ORG-10292',
      organizationName: 'ABC Heavy Industries Sourcing',
      industry: 'Infrastructure & EPC',
      usersCount: 18,
      suppliersCount: 291,
      verificationRequestsCount: 489,
      status: 'ACTIVE',
      joinedDate: '22 Feb 2026'
    },
    {
      orgId: 'ORG-10293',
      organizationName: 'Tata Telematics Supply Chain',
      industry: 'Telecommunications',
      usersCount: 64,
      suppliersCount: 1420,
      verificationRequestsCount: 2310,
      status: 'ACTIVE',
      joinedDate: '05 Mar 2026'
    },
    {
      orgId: 'ORG-10294',
      organizationName: 'Apex Defense Technologies Procurement',
      industry: 'Aerospace & Defense',
      usersCount: 8,
      suppliersCount: 84,
      verificationRequestsCount: 112,
      status: 'PENDING',
      joinedDate: '01 Sep 2026'
    }
  ]);

  // A4: Operations & Pipeline
  operationsMetrics = signal({
    pending: 4210,
    inReview: 1184,
    approved: 62841,
    rejected: 8412,
    escalated: 421,
    slaBreached: 118,
    pipeline: [
      { stage: 'Submitted', count: 8412, percentage: 100 },
      { stage: 'In Review', count: 4210, percentage: 50 },
      { stage: 'Approved', count: 6841, percentage: 81 },
      { stage: 'Rejected', count: 1182, percentage: 14 }
    ],
    verifierWorkload: [
      { verifier: 'Rahul Sharma', active: 14, today: 42, sla: '98%', role: 'Senior Compliance' },
      { Priya: 'Priya Singh', active: 11, today: 38, sla: '96%', role: 'Compliance Officer' },
      { Amit: 'Amit Kumar', active: 18, today: 44, sla: '91%', role: 'Compliance Officer' }
    ],
    queueHealth: [
      { type: 'GST', pending: 1281 },
      { type: 'PAN', pending: 821 },
      { type: 'Address', pending: 612 },
      { type: 'Bank', pending: 421 },
      { type: 'Certification', pending: 312 },
      { type: 'Business Identity', pending: 763 }
    ]
  });

  // A5: Verification Rules
  private rulesSignal = signal<VerificationRule[]>([
    {
      id: 'RUL-001',
      name: 'GST Registration Required',
      type: 'Identity',
      version: 'v3',
      status: 'ACTIVE',
      appliesTo: 'Business Identity',
      whenField: 'Credential Type',
      whenOperator: 'equals',
      whenValue: 'GST',
      andField: 'Business Type',
      andOperator: 'not equals',
      andValue: 'Individual',
      thenAction: 'Require Verification',
      failureAction: 'Create Verification Case',
      priority: 'HIGH',
      createdBy: 'Compliance Admin',
      createdAt: '01 Aug 2026',
      effectiveFrom: '01 Aug 2026',
      changeReason: 'Updated to mandate GSTN live portal check for all corporate applicants.'
    },
    {
      id: 'RUL-002',
      name: 'PAN Match Mandate',
      type: 'Identity',
      version: 'v2',
      status: 'ACTIVE',
      appliesTo: 'Direct Tax Credential',
      whenField: 'Credential Type',
      whenOperator: 'equals',
      whenValue: 'PAN',
      thenAction: 'Query CBDT NSDL Registry',
      failureAction: 'Flag Identity Mismatch',
      priority: 'HIGH',
      createdBy: 'System Architect',
      createdAt: '15 Jul 2026',
      effectiveFrom: '15 Jul 2026'
    },
    {
      id: 'RUL-003',
      name: 'ISO 9001 Expiry Window',
      type: 'Compliance',
      version: 'v1',
      status: 'ACTIVE',
      appliesTo: 'Quality Certifications',
      whenField: 'Certificate Expiry',
      whenOperator: 'less than',
      whenValue: '30 Days',
      thenAction: 'Trigger Reverification Notification',
      failureAction: 'Degrade Trust Index',
      priority: 'MEDIUM',
      createdBy: 'Quality Head',
      createdAt: '10 Jun 2026',
      effectiveFrom: '10 Jun 2026'
    },
    {
      id: 'RUL-004',
      name: 'Risk Score Threshold Escalation',
      type: 'Risk',
      version: 'v4',
      status: 'ACTIVE',
      appliesTo: 'Heuristic Risk Engine',
      whenField: 'Composite Risk Score',
      whenOperator: 'greater than',
      whenValue: '75',
      thenAction: 'Route to Senior Escalation Queue',
      failureAction: 'Block Autonomous Clearance',
      priority: 'HIGH',
      createdBy: 'Chief Risk Officer',
      createdAt: '28 Aug 2026',
      effectiveFrom: '01 Sep 2026',
      changeReason: 'Lowered threshold from 80 to 75 to catch subtle altered invoice/cheque patterns.'
    }
  ]);

  // A6: Credential Types Catalog
  private credentialsSignal = signal<CredentialTypeConfig[]>([
    {
      code: 'GST',
      displayName: 'GST Registration (GSTIN)',
      category: 'Tax',
      status: 'ACTIVE',
      verificationMethod: 'API',
      hasExpiry: true,
      required: true,
      sources: { api: true, document: true, manual: false },
      reverificationPeriod: '30 days'
    },
    {
      code: 'PAN',
      displayName: 'Permanent Account Number (PAN)',
      category: 'Identity',
      status: 'ACTIVE',
      verificationMethod: 'API',
      hasExpiry: false,
      required: true,
      sources: { api: true, document: true, manual: false },
      reverificationPeriod: 'Never (Statutory)'
    },
    {
      code: 'CIN',
      displayName: 'Corporate Identification Number (CIN)',
      category: 'Corporate',
      status: 'ACTIVE',
      verificationMethod: 'API',
      hasExpiry: false,
      required: true,
      sources: { api: true, document: false, manual: false },
      reverificationPeriod: '90 days'
    },
    {
      code: 'UDYAM',
      displayName: 'MSME Udyam Registration',
      category: 'Identity',
      status: 'ACTIVE',
      verificationMethod: 'API',
      hasExpiry: false,
      required: false,
      sources: { api: true, document: true, manual: false },
      reverificationPeriod: '180 days'
    },
    {
      code: 'ISO',
      displayName: 'ISO Quality Certification (IAF)',
      category: 'Compliance',
      status: 'ACTIVE',
      verificationMethod: 'API',
      hasExpiry: true,
      required: false,
      sources: { api: true, document: true, manual: false },
      reverificationPeriod: '365 days'
    },
    {
      code: 'BANK',
      displayName: 'Bank Account & Penny-Drop Verification',
      category: 'Banking',
      status: 'ACTIVE',
      verificationMethod: 'API',
      hasExpiry: false,
      required: true,
      sources: { api: true, document: true, manual: true },
      reverificationPeriod: '90 days'
    },
    {
      code: 'FSSAI',
      displayName: 'Food Safety License (FSSAI)',
      category: 'Compliance',
      status: 'ACTIVE',
      verificationMethod: 'API',
      hasExpiry: true,
      required: false,
      sources: { api: true, document: true, manual: false },
      reverificationPeriod: '365 days'
    },
    {
      code: 'IEC',
      displayName: 'Importer Exporter Code (DGFT)',
      category: 'Corporate',
      status: 'ACTIVE',
      verificationMethod: 'API',
      hasExpiry: false,
      required: false,
      sources: { api: true, document: false, manual: false },
      reverificationPeriod: '180 days'
    }
  ]);

  // A7: Document Security & Lifecycle
  private documentsSignal = signal<AdminDocument[]>([
    {
      id: 'DOC-8291',
      fileName: 'GST_Certificate_ABC_Industrial.pdf',
      businessName: 'ABC INDUSTRIAL DYNAMICS',
      passportId: 'VP-IN-2026-92644',
      credentialType: 'GST',
      fileType: 'PDF',
      uploadedAt: '06 Sep 2026 · 10:21 AM',
      status: 'VERIFIED',
      risk: 'LOW',
      sha256: '8b2f90a13c4d7e89123b56cf8912e45a789c01de23f45a67b89c01de23f45a67',
      malwareScanResult: 'Clean (ClamAV + SHA-256 Validated)'
    },
    {
      id: 'DOC-8292',
      fileName: 'ISO_Certificate_Axionis.pdf',
      businessName: 'Axionis manufacturing pvt ltd',
      passportId: 'VP-IN-2026-46190',
      credentialType: 'ISO',
      fileType: 'PDF',
      uploadedAt: '06 Sep 2026 · 09:42 AM',
      status: 'VERIFIED',
      risk: 'LOW',
      sha256: '9c4f12e84a5b6c7d8e9f01a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2',
      malwareScanResult: 'Clean (IAF CertSearch Validated)'
    },
    {
      id: 'DOC-8293',
      fileName: 'Bank_Proof_ABC_Industrial.pdf',
      businessName: 'ABC INDUSTRIAL DYNAMICS',
      passportId: 'VP-IN-2026-92644',
      credentialType: 'Bank',
      fileType: 'PDF',
      uploadedAt: '07 Oct 2026 · 10:15 IST',
      status: 'VERIFIED',
      risk: 'LOW',
      sha256: '778899aabbccddeeff00112233445566778899aabbccddeeff00112233445566',
      malwareScanResult: 'Clean (NPCI Penny Drop Validated)'
    },
    {
      id: 'DOC-8294',
      fileName: 'PAN_Card_Axionis.pdf',
      businessName: 'Axionis manufacturing pvt ltd',
      passportId: 'VP-IN-2026-46190',
      credentialType: 'PAN',
      fileType: 'PDF',
      uploadedAt: '07 Oct 2026 · 11:30 IST',
      status: 'VERIFIED',
      risk: 'LOW',
      sha256: '44556677889900112233445566778899aabbccddeeff00112233445566778899',
      malwareScanResult: 'Clean (NSDL Validated)'
    }
  ]);

  documentStats = signal({
    total: 241821,
    processing: 2184,
    verified: 181421,
    rejected: 31291,
    quarantined: 421,
    expired: 26504
  });

  // A8: Platform Risk Intelligence
  riskDistribution = signal({
    high: 421,
    medium: 1890,
    low: 81980,
    categories: [
      { name: 'Identity Mismatch', count: 182 },
      { name: 'Document Alteration', count: 121 },
      { name: 'Duplicate Credentials', count: 97 },
      { name: 'Expired Credentials', count: 81 },
      { name: 'Unusual Volume Activity', count: 63 },
      { name: 'Sovereign Source Mismatch', count: 42 },
      { name: 'Repeated Verification Failures', count: 31 }
    ]
  });

  // A9: Disputes Queue
  private disputesSignal = signal<AdminDispute[]>([
    {
      id: 'DSP-1821',
      businessName: 'ABC INDUSTRIAL DYNAMICS',
      passportId: 'VP-IN-2026-92644',
      originalCaseId: 'VP-18291',
      credentialType: 'GST',
      reason: 'Address variance reconciled',
      supplierStatement: 'GST address verified against plant premises registration.',
      originalDecision: 'RESOLVED',
      previousVerifier: 'Rahul Sharma',
      status: 'RESOLVED',
      openedAt: '06 Sep 2026 · 11:40 IST',
      evidenceExamined: ['GST Certificate PDF', 'GSTN API Response', 'OCR Result']
    },
    {
      id: 'DSP-1822',
      businessName: 'Axionis manufacturing pvt ltd',
      passportId: 'VP-IN-2026-46190',
      originalCaseId: 'VP-18292',
      credentialType: 'PAN',
      reason: 'Name verification confirmed',
      supplierStatement: 'NSDL statutory title successfully matched with incorporation registry.',
      originalDecision: 'RESOLVED',
      previousVerifier: 'Priya Singh',
      status: 'RESOLVED',
      openedAt: '05 Sep 2026 · 16:15 IST',
      evidenceExamined: ['MCA Certificate', 'PAN Card Scan']
    }
  ]);

  // A10: Audit Logs
  private auditLogsSignal = signal<AdminAuditLog[]>([
    {
      id: 'AUD-918273',
      time: '06 Sep 2026 · 13:21 IST',
      actor: 'Rahul Sharma',
      role: 'SENIOR_VERIFIER',
      action: 'APPROVE_CREDENTIAL',
      resource: 'VP-18291',
      result: 'SUCCESS',
      ip: '10.240.12.84',
      correlationId: 'REQ-819281',
      reason: 'All sovereign GSTN records matched certified incorporation proof.',
      previousState: 'IN_REVIEW',
      newState: 'APPROVED'
    },
    {
      id: 'AUD-918272',
      time: '06 Sep 2026 · 13:19 IST',
      actor: 'admin@veripass.in',
      role: 'PLATFORM_ADMIN',
      action: 'BUSINESS_SUSPENDED',
      resource: 'VP-DLT9942',
      result: 'SUCCESS',
      ip: '192.168.1.104',
      correlationId: 'REQ-819280',
      reason: 'Statutory policy violation: payee mismatch on penny-drop validation.',
      previousState: 'FLAGGED',
      newState: 'SUSPENDED'
    },
    {
      id: 'AUD-918271',
      time: '06 Sep 2026 · 13:12 IST',
      actor: 'Priya Singh',
      role: 'COMPLIANCE_OFFICER',
      action: 'REJECT_CREDENTIAL',
      resource: 'VP-18282',
      result: 'SUCCESS',
      ip: '10.240.12.89',
      correlationId: 'REQ-819279',
      reason: 'Invalid DIN Director identification on MCA portal.',
      previousState: 'IN_REVIEW',
      newState: 'REJECTED'
    },
    {
      id: 'AUD-918270',
      time: '06 Sep 2026 · 12:45 IST',
      actor: 'admin@veripass.in',
      role: 'PLATFORM_ADMIN',
      action: 'RULE_PUBLISHED',
      resource: 'RUL-004',
      result: 'SUCCESS',
      ip: '192.168.1.104',
      correlationId: 'REQ-819275',
      reason: 'Published v4 of Risk Score Threshold escalation policy.',
      previousState: 'DRAFT',
      newState: 'ACTIVE'
    }
  ]);

  // A12: Users & Roles
  private usersSignal = signal<AdminUser[]>([
    { userId: 'USR-1821', name: 'Rahul Sharma', email: 'rahul.s@veripass.in', role: 'SENIOR_VERIFIER', status: 'ACTIVE', lastLogin: 'Today · 09:11 IST' },
    { userId: 'USR-1822', name: 'Priya Singh', email: 'priya.s@veripass.in', role: 'VERIFIER', status: 'ACTIVE', lastLogin: 'Today · 08:45 IST' },
    { userId: 'USR-1823', name: 'Amit Kumar', email: 'amit.k@veripass.in', role: 'VERIFIER', status: 'ACTIVE', lastLogin: 'Today · 09:30 IST' },
    { userId: 'USR-1824', name: 'Sanjay Mehta', email: 'admin@veripass.in', role: 'PLATFORM_ADMIN', status: 'ACTIVE', lastLogin: 'Today · 13:00 IST' },
    { userId: 'USR-1825', name: 'Neha Varma', email: 'neha.v@xyzcorp.com', role: 'BUYER_ADMIN', status: 'ACTIVE', lastLogin: 'Yesterday · 17:20 IST' },
    { userId: 'USR-1826', name: 'Vikram Patel', email: 'vikram@abcmfg.in', role: 'SUPPLIER', status: 'ACTIVE', lastLogin: '04 Sep · 14:12 IST' }
  ]);

  // A14: Security Sessions
  private sessionsSignal = signal<AdminSession[]>([
    { id: 'SESS-01', user: 'admin@veripass.in', device: 'Chrome 128 / Windows 11 Enterprise', location: 'New Delhi, India', lastActive: 'Active now (2m ago)', ip: '192.168.1.104', isCurrent: true },
    { id: 'SESS-02', user: 'compliance.admin@veripass.in', device: 'Edge 128 / Windows 11', location: 'Noida, India', lastActive: '7m ago', ip: '10.240.12.55', isCurrent: false },
    { id: 'SESS-03', user: 'security.admin@veripass.in', device: 'Safari / macOS Sonoma', location: 'Bengaluru, India', lastActive: '18m ago', ip: '10.240.12.92', isCurrent: false }
  ]);

  // A13: System Configuration
  private systemConfigSignal = signal<SystemConfiguration>({
    general: {
      platformName: 'Veripass Sovereign Trust Engine',
      supportEmail: 'ops@veripass.in',
      maintenanceMode: false,
      allowPublicRegistrations: true,
      defaultTimezone: 'Asia/Kolkata (IST +05:30)'
    },
    authentication: {
      enforceMfaAllAdmins: true,
      allowSmsMfaForStaff: false,
      sessionInactivityTimeoutMinutes: 15,
      maxConcurrentSessionsPerUser: 2,
      passwordExpiryDays: 90
    },
    verification: {
      requireHumanDecision: true, // Strictly locked true
      minOcrConfidencePercent: 85,
      minScoreForAutoPrecheck: 80,
      validityPeriodMonths: 12,
      maxDisputeFilingWindowDays: 30
    },
    integrations: [
      { id: 'INT-01', name: 'GSTN Sovereign Gateway', endpoint: 'https://api.gst.gov.in/v1.2', status: 'ONLINE', latencyMs: 210, lastPing: '30s ago', protocol: 'mTLS REST v1.2' },
      { id: 'INT-02', name: 'MCA21 / V3 Master Registry', endpoint: 'https://mca.gov.in/v3/company-master', status: 'ONLINE', latencyMs: 340, lastPing: '45s ago', protocol: 'SOAP / XML' },
      { id: 'INT-03', name: 'CBDT PAN Verification API', endpoint: 'https://pan.tin.nsdl.com/api/v2', status: 'ONLINE', latencyMs: 180, lastPing: '1m ago', protocol: 'HMAC REST' },
      { id: 'INT-04', name: 'DigiLocker Trust Vault', endpoint: 'https://api.digilocker.gov.in/oauth2/1/xml', status: 'ONLINE', latencyMs: 145, lastPing: '20s ago', protocol: 'OAuth2 / SAML' },
      { id: 'INT-05', name: 'NPCI Penny-Drop Interbank Bridge', endpoint: 'https://api.npci.org.in/imps/v2/verify', status: 'ONLINE', latencyMs: 410, lastPing: '1m ago', protocol: 'ISO 20022 / mTLS' }
    ]
  });

  // A14: Security Alerts
  private securityAlertsSignal = signal<SecurityEventAlert[]>([
    {
      id: 'SEC-901',
      timestamp: '06 Sep 2026 · 13:35 IST',
      severity: 'CRITICAL',
      title: 'Multiple failed MFA challenge attempts',
      description: '6 consecutive invalid TOTP tokens submitted from unauthorized IP 185.220.101.5 (Tor Exit Node). Account locked automatically.',
      sourceIp: '185.220.101.5',
      affectedAccount: 'verifier.ops@veripass.in',
      status: 'INVESTIGATING'
    },
    {
      id: 'SEC-902',
      timestamp: '06 Sep 2026 · 12:10 IST',
      severity: 'MEDIUM',
      title: 'Anomalous export volume threshold',
      description: 'Export of 500+ immutable audit records requested. Signature and biometric MFA verified.',
      sourceIp: '10.240.12.84',
      affectedAccount: 'rahul.s@veripass.in',
      status: 'RESOLVED'
    },
    {
      id: 'SEC-903',
      timestamp: '05 Sep 2026 · 22:45 IST',
      severity: 'LOW',
      title: 'Hardware security token challenge',
      description: 'Platform Admin login from hardware FIDO2 / WebAuthn security key validated.',
      sourceIp: '10.240.12.92',
      affectedAccount: 'security.admin@veripass.in',
      status: 'RESOLVED'
    }
  ]);

  // Read-only computed signals
  businesses = computed(() => this.businessesSignal());
  buyers = computed(() => this.buyersSignal());
  rules = computed(() => this.rulesSignal());
  credentials = computed(() => this.credentialsSignal());
  documents = computed(() => this.documentsSignal());
  disputes = computed(() => this.disputesSignal());
  auditLogs = computed(() => this.auditLogsSignal());
  users = computed(() => this.usersSignal());
  sessions = computed(() => this.sessionsSignal());
  systemConfig = computed(() => this.systemConfigSignal());
  securityAlerts = computed(() => this.securityAlertsSignal());

  // Mutator Actions
  flagBusiness(passportId: string, reason: string): void {
    this.businessesSignal.update(all =>
      all.map(b => (b.passportId === passportId ? { ...b, status: 'FLAGGED', flagReason: reason } : b))
    );
    this.recordAudit('BUSINESS_FLAGGED', passportId, `Flagged for investigation: ${reason}`, 'ACTIVE', 'FLAGGED');

    this.api.post<{ success: boolean }>(`admin/businesses/${encodeURIComponent(passportId)}/flag`, { reason }).subscribe({
      next: () => this.loadBusinesses(),
      error: (err) => console.warn('Server sync failed for flagBusiness:', err)
    });
  }

  suspendBusiness(passportId: string, reason: string): void {
    const prev = this.businessesSignal().find(b => b.passportId === passportId)?.status || 'ACTIVE';
    this.businessesSignal.update(all =>
      all.map(b => (b.passportId === passportId ? { ...b, status: 'SUSPENDED', suspensionReason: reason } : b))
    );
    this.recordAudit('BUSINESS_SUSPENDED', passportId, reason, prev, 'SUSPENDED');

    this.api.post<{ success: boolean }>(`admin/businesses/${encodeURIComponent(passportId)}/suspend`, { reason }).subscribe({
      next: () => this.loadBusinesses(),
      error: (err) => console.warn('Server sync failed for suspendBusiness:', err)
    });
  }

  reactivateBusiness(passportId: string): void {
    const prev = this.businessesSignal().find(b => b.passportId === passportId)?.status || 'SUSPENDED';
    this.businessesSignal.update(all =>
      all.map(b => (b.passportId === passportId ? { ...b, status: 'ACTIVE', suspensionReason: undefined, flagReason: undefined } : b))
    );
    this.recordAudit('BUSINESS_REACTIVATED', passportId, 'Compliance verification satisfied. Business reinstated.', prev, 'ACTIVE');

    this.api.post<{ success: boolean }>(`admin/businesses/${encodeURIComponent(passportId)}/activate`, {}).subscribe({
      next: () => this.loadBusinesses(),
      error: (err) => console.warn('Server sync failed for reactivateBusiness:', err)
    });
  }

  suspendBuyer(orgId: string, reason: string): void {
    this.buyersSignal.update(all =>
      all.map(b => (b.orgId === orgId ? { ...b, status: 'SUSPENDED', suspensionReason: reason } : b))
    );
    this.recordAudit('BUYER_SUSPENDED', orgId, reason, 'ACTIVE', 'SUSPENDED');
  }

  reactivateBuyer(orgId: string): void {
    this.buyersSignal.update(all =>
      all.map(b => (b.orgId === orgId ? { ...b, status: 'ACTIVE', suspensionReason: undefined } : b))
    );
    this.recordAudit('BUYER_REACTIVATED', orgId, 'Buyer account reinstated.', 'SUSPENDED', 'ACTIVE');
  }

  createRule(rule: Omit<VerificationRule, 'id' | 'createdAt' | 'createdBy'>): void {
    const newId = 'RUL-00' + (this.rulesSignal().length + 1);
    const newRule: VerificationRule = {
      ...rule,
      id: newId,
      createdAt: '06 Sep 2026',
      createdBy: 'Compliance Admin'
    };
    this.rulesSignal.update(all => [newRule, ...all]);
    this.recordAudit('RULE_CREATED', newId, `Rule created: ${newRule.name} (${newRule.version})`, 'NONE', newRule.status);
  }

  publishRule(ruleId: string, reason: string): void {
    this.rulesSignal.update(all =>
      all.map(r => (r.id === ruleId ? { ...r, status: 'ACTIVE', changeReason: reason } : r))
    );
    this.recordAudit('RULE_PUBLISHED', ruleId, reason, 'DRAFT', 'ACTIVE');
  }

  updateCredentialConfig(code: string, config: Partial<CredentialTypeConfig>): void {
    this.credentialsSignal.update(all =>
      all.map(c => (c.code === code ? { ...c, ...config } : c))
    );
    this.recordAudit('CREDENTIAL_CONFIG_UPDATED', code, 'Credential schema parameters updated', 'ACTIVE', 'ACTIVE');
  }

  quarantineDocument(docId: string, reason: string): void {
    this.documentsSignal.update(all =>
      all.map(d => (d.id === docId ? { ...d, status: 'QUARANTINE', quarantineReason: reason, risk: 'HIGH' } : d))
    );
    this.recordAudit('DOCUMENT_QUARANTINED', docId, reason, 'PROCESSING', 'QUARANTINE');
  }

  resolveDispute(disputeId: string, action: 'UPHOLD' | 'REVERIFY' | 'ESCALATE', reason: string): void {
    const disp = this.disputesSignal().find(d => d.id === disputeId);
    if (!disp) return;

    this.disputesSignal.update(all =>
      all.map(d =>
        d.id === disputeId
          ? {
              ...d,
              status: 'RESOLVED',
              resolutionAction: action,
              resolutionReason: reason,
              resolvedAt: '06 Sep 2026 · 13:40 IST'
            }
          : d
      )
    );

    this.recordAudit(
      `DISPUTE_${action}`,
      disp.id,
      `Dispute adjudicated: ${action} — ${reason}. Original case: ${disp.originalCaseId}`,
      disp.status,
      'RESOLVED'
    );
  }

  updateGeneralConfig(patch: Partial<SystemConfiguration['general']>): void {
    this.systemConfigSignal.update(cfg => ({
      ...cfg,
      general: { ...cfg.general, ...patch }
    }));
    this.recordAudit('CONFIG_UPDATED', 'SYS_GENERAL', 'Updated general system parameters', 'ACTIVE', 'ACTIVE');
  }

  updateAuthConfig(patch: Partial<SystemConfiguration['authentication']>): void {
    this.systemConfigSignal.update(cfg => ({
      ...cfg,
      authentication: { ...cfg.authentication, ...patch }
    }));
    this.recordAudit('CONFIG_UPDATED', 'SYS_AUTH_MFA', 'Updated authentication security policies', 'ACTIVE', 'ACTIVE');
  }

  updateVerificationConfig(patch: Partial<SystemConfiguration['verification']>): void {
    // Safety guardrail: Require human decision must strictly remain true in production
    const safePatch = { ...patch, requireHumanDecision: true };
    this.systemConfigSignal.update(cfg => ({
      ...cfg,
      verification: { ...cfg.verification, ...safePatch }
    }));
    this.recordAudit('CONFIG_UPDATED', 'SYS_VERIFICATION', 'Updated automated verification thresholds and validity period', 'ACTIVE', 'ACTIVE');
  }

  resolveSecurityAlert(alertId: string, resolution: string): void {
    this.securityAlertsSignal.update(all =>
      all.map(a => (a.id === alertId ? { ...a, status: 'RESOLVED' as const } : a))
    );
    this.recordAudit('SECURITY_ALERT_RESOLVED', alertId, `Alert resolved: ${resolution}`, 'INVESTIGATING', 'RESOLVED');
  }

  revokeSession(sessionId: string): void {
    this.sessionsSignal.update(all => all.filter(s => s.id !== sessionId));
    this.recordAudit('SESSION_REVOKED', sessionId, 'Session revoked by Administrator', 'ACTIVE', 'TERMINATED');
  }

  revokeAllOtherSessions(): void {
    this.sessionsSignal.update(all => all.filter(s => s.isCurrent));
    this.recordAudit('ALL_SESSIONS_REVOKED', 'ADMIN_SOC', 'Terminated all concurrent administrative sessions', 'ACTIVE', 'TERMINATED');
  }

  private recordAudit(action: string, resource: string, reason: string, prev: string, next: string): void {
    const newEntry: AdminAuditLog = {
      id: 'AUD-' + Math.floor(900000 + Math.random() * 99999),
      time: '06 Sep 2026 · ' + new Date().toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit' }) + ' IST',
      actor: 'admin@veripass.in',
      role: 'PLATFORM_ADMIN',
      action,
      resource,
      result: 'SUCCESS',
      ip: '192.168.1.104',
      correlationId: 'REQ-' + Math.floor(800000 + Math.random() * 99999),
      reason,
      previousState: prev,
      newState: next
    };
    this.auditLogsSignal.update(all => [newEntry, ...all]);
  }
}
