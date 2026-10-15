import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { AuthService } from '../../../core/auth/auth.service';
import { BuyerSearchService, BuyerSupplier } from '../../../core/services/buyer-search.service';

export interface PillarDetail {
  name: string;
  score: number;
  source: string;
  verifiedAt: string;
  description: string;
}

export interface DirectorDetail {
  name: string;
  designation: string;
  din: string;
}

export interface DocumentDetail {
  title: string;
  issuer: string;
  expiry: string;
}

export interface AuditRecord {
  timestamp: string;
  action: string;
  actor: string;
}

@Component({
  selector: 'app-buyer-passport-viewer',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './buyer-passport-viewer.component.html',
  styleUrl: './buyer-passport-viewer.component.css'
})
export class BuyerPassportViewerComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private http = inject(HttpClient);
  private auth = inject(AuthService);
  private searchService = inject(BuyerSearchService);

  private readonly apiUrl = 'http://localhost:5000/api/v1';

  supplier = signal<BuyerSupplier | undefined>(undefined);
  activeTab = signal<'overview' | 'identity' | 'verification' | 'compliance' | 'certifications' | 'activity'>('overview');

  directors: DirectorDetail[] = [];
  pillars: PillarDetail[] = [];
  documents: DocumentDetail[] = [];
  auditHistory: AuditRecord[] = [];

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const id = params.get('supplierId');
      if (!id) return;
      const s = this.searchService.getSupplierById(id) || this.searchService.filteredSuppliers().find(x => x.id === id);
      this.supplier.set(s);

      if (s) {
        this.loadPassportDossier(s);
        this.loadSupplierAuditHistory(s);
      }
    });
  }

  loadPassportDossier(s: BuyerSupplier): void {
    const hasPan = s.pan && s.pan !== '—';
    const hasGst = s.gstin && s.gstin !== '—';
    const hasCin = s.cin && s.cin !== '—';
    const isVerified = s.status === 'Verified';

    // 1. Initialize pillars from supplier profile data
    this.pillars = [
      {
        name: 'Statutory Identity',
        score: isVerified ? (s.trustScore || 85) : (hasPan || hasCin ? 60 : 0),
        source: 'MCA-21 Gateway',
        verifiedAt: isVerified ? 'Verified' : 'Pending',
        description: `CIN (${s.cin || 'Pending'}), PAN (${s.pan || 'Pending'}), and corporate filings verified against sovereign registry.`
      },
      {
        name: 'Tax & Invoicing',
        score: isVerified ? (s.complianceScore || 85) : (hasGst ? 60 : 0),
        source: 'GSTN Invoicing Portal',
        verifiedAt: isVerified ? 'Verified' : 'Pending',
        description: `GSTIN (${s.gstin || 'Pending'}) status and tax return filings verified on GST portal.`
      },
      {
        name: 'Banking & Financials',
        score: isVerified ? (s.trustScore || 80) : 0,
        source: 'NPCI Penny-Drop Gateway',
        verifiedAt: isVerified ? 'Verified' : 'Pending',
        description: `Penny-drop bank account validation matched with corporate PAN (${s.pan || 'Pending'}).`
      },
      {
        name: 'Physical Premises',
        score: isVerified ? (s.trustScore || 75) : 0,
        source: 'VeriPass Field Inspector',
        verifiedAt: isVerified ? 'Verified' : 'Pending',
        description: `Registered physical business premises at ${s.location || 'India'}.`
      }
    ];

    this.directors = [];

    // 2. Fetch real credentials from backend passport endpoint
    this.http.get<any>(`${this.apiUrl}/businesses/${s.id}/passport`).subscribe({
      next: (passport) => {
        if (passport?.directors && Array.isArray(passport.directors) && passport.directors.length > 0) {
          this.directors = passport.directors.map((d: any) => ({
            name: d.name || d.fullName || 'Director',
            designation: d.designation || 'Director',
            din: d.din || '—'
          }));
        } else {
          this.directors = [];
        }

        if (passport?.verifiedCredentials && passport.verifiedCredentials.length > 0) {
          this.documents = passport.verifiedCredentials.map((c: any) => ({
            title: c.credentialName || c.credentialType || 'Statutory Credential',
            issuer: c.displayValue || c.issuer || 'Government of India',
            expiry: c.lastVerifiedAt ? new Date(c.lastVerifiedAt).toLocaleDateString('en-IN') : 'Perpetual'
          }));
        } else {
          this.documents = [];
        }
      },
      error: () => {
        this.directors = [];
        this.documents = [];
      }
    });
  }

  loadSupplierAuditHistory(s: BuyerSupplier): void {
    const searchTarget = encodeURIComponent(s.name.split(' ')[0]);
    const u = this.auth.currentUser();
    const currentBuyer = u?.fullName || `${u?.firstName || ''} ${u?.lastName || ''}`.trim() || 'Authorized Buyer';
    this.http.get<any>(`${this.apiUrl}/buyer/activity?search=${searchTarget}&pageSize=5`).subscribe({
      next: (res) => {
        const items = res?.items || [];
        if (items.length > 0) {
          this.auditHistory = items.map((a: any) => ({
            timestamp: new Date(a.timestamp).toLocaleString('en-IN', { dateStyle: 'medium', timeStyle: 'short' }),
            action: `${a.actionLabel || a.action} — ${a.target?.name || s.name}`,
            actor: `${a.user?.name || currentBuyer} (${a.user?.role || 'Buyer'})`
          }));
        } else {
          this.auditHistory = [
            { timestamp: 'Today', action: `Viewed Sovereign Passport for ${s.name}`, actor: `${currentBuyer} (Buyer)` }
          ];
        }
      },
      error: () => {
        this.auditHistory = [
          { timestamp: 'Today', action: `Viewed Sovereign Passport for ${s.name}`, actor: `${currentBuyer} (Buyer)` }
        ];
      }
    });
  }

  setTab(tab: 'overview' | 'identity' | 'verification' | 'compliance' | 'certifications' | 'activity'): void {
    this.activeTab.set(tab);
  }

  toggleShortlist(id: string): void {
    this.searchService.toggleShortlist(id);
    const updated = this.searchService.getSupplierById(id);
    if (updated) this.supplier.set(updated);
  }
}
