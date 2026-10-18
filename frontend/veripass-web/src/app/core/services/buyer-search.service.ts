import { Injectable, signal, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';

export interface BuyerSupplier {
  id: string;
  name: string;
  legalName: string;
  category: string;
  industry: string;
  location: string;
  state: string;
  businessType: string;
  trustScore: number;
  complianceScore: number;
  yearsActive: number;
  riskLevel: 'Low' | 'Medium' | 'High';
  status: 'Verified' | 'Partially Verified' | 'Expired';
  gstin: string;
  pan: string;
  cin: string;
  udyam: string;
  certifications: string[];
  lastVerified: string;
  expiringCredential?: string;
  isShortlisted?: boolean;
  complianceStatus?: 'Compliant' | 'Incomplete' | 'VerificationFailed' | string;
  trustLevel?: 'Low' | 'Medium' | 'High' | string;
  isSelectable?: boolean;
}

export interface SearchFilters {
  industries: string[];
  state: string;
  businessTypes: string[];
  verificationStatus: string;
  minTrustScore: number;
  certifications: string[];
  complianceStanding: string;
  minYears: number;
}

export interface ParsedQueryTokens {
  industry?: string;
  location?: string;
  certification?: string;
  active: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class BuyerSearchService {
  private http = inject(HttpClient);
  private readonly apiUrl = 'http://localhost:5000/api/v1';

  // Master Supplier Catalog for Enterprise Procurement Intelligence (loaded dynamically from database)
  suppliers = signal<BuyerSupplier[]>([]);

  // Reactive Search & Filter State
  searchQuery = signal<string>('');
  
  parsedTokens = signal<ParsedQueryTokens>({
    industry: undefined,
    location: undefined,
    certification: undefined,
    active: false
  });

  filterState = signal<SearchFilters>({
    industries: [],
    state: '',
    businessTypes: [],
    verificationStatus: 'all',
    minTrustScore: 0,
    certifications: [],
    complianceStanding: 'all',
    minYears: 0
  });

  activeStatusTab = signal<'all' | 'verified' | 'partially_verified' | 'shortlisted' | 'flagged'>('all');
  selectedSort = signal<string>('score_desc');
  selectedForCompare = signal<string[]>([]);
  isLoading = signal<boolean>(false);

  constructor() {
    this.loadSuppliersFromBackend();
    this.restoreShortlistFromStorage();
  }

  // Load from backend /api/v1/suppliers and sync with /api/v1/shortlists
  loadSuppliersFromBackend(): void {
    this.isLoading.set(true);

    // Fetch shortlists from PostgreSQL to get authentic shortlist status
    this.http.get<any[]>(`${this.apiUrl}/shortlists`).subscribe({
      next: (collections) => {
        const backendShortlistIds = new Set<string>();
        if (Array.isArray(collections)) {
          for (const col of collections) {
            for (const item of col.items || []) {
              if (item.businessId) backendShortlistIds.add(item.businessId.toLowerCase());
            }
          }
        }
        this.fetchAndMergeSuppliers(backendShortlistIds);
      },
      error: () => {
        this.fetchAndMergeSuppliers(new Set());
      }
    });
  }

  private fetchAndMergeSuppliers(backendShortlistIds: Set<string>): void {
    this.http.get<any>(`${this.apiUrl}/suppliers`).subscribe({
      next: (res) => {
        this.isLoading.set(false);
        const list = Array.isArray(res) ? res : (res?.suppliers || res?.items || res?.value || []);
        if (list.length === 0) {
          this.suppliers.set([]);
          return;
        }

        const merged: BuyerSupplier[] = list.map((b: any) => {
          const bId = (b.businessId || b.id || '').toLowerCase();
          const industry = b.industry || this.detectIndustry(b.category || b.description || b.legalName);
          const location = b.city && b.state ? `${b.city}, ${b.state}` : (b.state || b.city || 'India');
          const status: 'Verified' | 'Partially Verified' | 'Expired' =
            b.verificationStatus === 'VERIFIED' ? 'Verified' :
            (b.verificationStatus === 'FLAGGED' || b.verificationStatus === 'EXPIRED' ? 'Expired' : 'Partially Verified');
          const score = b.trustScore ?? 0;
          let riskLevel: 'Low' | 'Medium' | 'High' = 'Medium';
          if (b.riskLevel) {
            const r = (b.riskLevel as string).toUpperCase();
            riskLevel = r === 'HIGH' ? 'High' : (r === 'LOW' ? 'Low' : 'Medium');
          } else {
            riskLevel = score < 35 ? 'High' : (score <= 70 ? 'Medium' : 'Low');
          }

          const isShortlisted = backendShortlistIds.has(bId) || this.isIdShortlisted(bId);

          return {
            id: b.businessId || b.id,
            name: b.tradeName || b.legalName,
            legalName: b.legalName,
            category: b.category || b.description || 'Industrial Manufacturing',
            industry: industry,
            location: location,
            state: b.state || '',
            businessType: b.businessType || 'Private Limited',
            trustScore: score,
            complianceScore: b.complianceScore ?? 0,
            yearsActive: b.yearsActive ?? 0,
            riskLevel: riskLevel,
            status: status,
            gstin: b.gstin || '—',
            pan: b.pan || '—',
            cin: b.cin || '—',
            udyam: b.udyamNumber || '—',
            certifications: b.verifiedCredentials && b.verifiedCredentials.length ? b.verifiedCredentials : [],
            lastVerified: b.verificationStatus === 'VERIFIED' ? 'Verified' : 'Pending',
            isShortlisted: isShortlisted
          };
        });

        // Query authoritative buyer trust score batch
        const supplierIds = merged.map(m => m.id).filter(id => !!id);
        if (supplierIds.length > 0) {
          this.http.post<any[]>(`${this.apiUrl}/buyers/me/trust-scores/batch`, { supplierIds }).subscribe({
            next: (scores) => {
              if (Array.isArray(scores)) {
                const map = new Map(scores.map(s => [s.supplierId?.toLowerCase(), s]));
                for (const sup of merged) {
                  const sc = map.get(sup.id?.toLowerCase());
                  if (sc) {
                    sup.trustScore = sc.score;
                    sup.complianceScore = sc.score;
                    sup.trustLevel = sc.trustLevel;
                    sup.complianceStatus = sc.complianceStatus;
                    sup.isSelectable = sc.isSelectable;
                    sup.riskLevel = sc.trustLevel === 'High' ? 'Low' : (sc.trustLevel === 'Medium' ? 'Medium' : 'High');
                  }
                }
              }
              this.suppliers.set(merged);
            },
            error: () => {
              this.suppliers.set(merged);
            }
          });
        } else {
          this.suppliers.set(merged);
        }
      },
      error: (err) => {
        this.isLoading.set(false);
        console.warn('Backend suppliers fetch notice:', err);
        this.suppliers.set([]);
      }
    });
  }

  private detectIndustry(desc: string = ''): string {
    const d = desc.toLowerCase();
    if (d.includes('electric') || d.includes('component')) return 'Electrical';
    if (d.includes('logistic') || d.includes('freight') || d.includes('fleet')) return 'Logistics';
    if (d.includes('construction') || d.includes('steel') || d.includes('composites')) return 'Construction';
    if (d.includes('software') || d.includes('semiconductor') || d.includes('it')) return 'IT Services';
    return 'Manufacturing';
  }

  // Computed Filtered Suppliers
  filteredSuppliers = computed(() => {
    const q = this.searchQuery().trim().toLowerCase();
    const f = this.filterState();
    const sort = this.selectedSort();
    const tab = this.activeStatusTab();

    let list = this.suppliers().filter(supplier => {
      // Tab filter
      if (tab === 'verified' && supplier.status !== 'Verified') return false;
      if (tab === 'partially_verified' && supplier.status !== 'Partially Verified') return false;
      if (tab === 'shortlisted' && !supplier.isShortlisted) return false;
      if (tab === 'flagged' && supplier.riskLevel.toUpperCase() !== 'HIGH' && supplier.status !== 'Expired' && !supplier.status.toLowerCase().includes('flagged')) return false;

      // Free text search matching name, legalName, industry, category, location, gstin, pan
      if (q) {
        const matchesQuery =
          supplier.name.toLowerCase().includes(q) ||
          supplier.legalName.toLowerCase().includes(q) ||
          supplier.category.toLowerCase().includes(q) ||
          supplier.industry.toLowerCase().includes(q) ||
          supplier.location.toLowerCase().includes(q) ||
          supplier.gstin.toLowerCase().includes(q) ||
          supplier.pan.toLowerCase().includes(q);

        if (!matchesQuery) return false;
      }

      // Industry filter
      if (f.industries.length > 0) {
        if (!f.industries.includes(supplier.industry)) return false;
      }

      // State location filter
      if (f.state && f.state !== '' && f.state !== 'All Regions (Pan India)') {
        if (!supplier.state.toLowerCase().includes(f.state.toLowerCase()) && !supplier.location.toLowerCase().includes(f.state.toLowerCase())) {
          return false;
        }
      }

      // Business Type filter
      if (f.businessTypes.length > 0) {
        const matched = f.businessTypes.some(bt => supplier.businessType.toLowerCase().includes(bt.toLowerCase()));
        if (!matched) return false;
      }

      // Verification Status from Sidebar
      if (f.verificationStatus === 'verified' && supplier.status !== 'Verified') {
        return false;
      }
      if (f.verificationStatus === 'partially_verified' && supplier.status !== 'Partially Verified') {
        return false;
      }

      // Trust Score Slider
      if (supplier.trustScore < f.minTrustScore) {
        return false;
      }

      // Certifications
      if (f.certifications.length > 0) {
        const hasAll = f.certifications.every(c =>
          supplier.certifications.some(sc => sc.toLowerCase().includes(c.toLowerCase()))
        );
        if (!hasAll) return false;
      }

      // Years Active
      if (supplier.yearsActive < f.minYears) {
        return false;
      }

      // Compliance Standing
      if (f.complianceStanding === 'good' && supplier.complianceScore < 85) return false;
      if (f.complianceStanding === 'expiring' && !supplier.expiringCredential) return false;

      return true;
    });

    // Sorting
    return list.sort((a, b) => {
      if (sort === 'score_desc') return b.trustScore - a.trustScore;
      if (sort === 'score_asc') return a.trustScore - b.trustScore;
      if (sort === 'compliance_desc') return b.complianceScore - a.complianceScore;
      if (sort === 'years_desc') return b.yearsActive - a.yearsActive;
      if (sort === 'name_asc') return a.name.localeCompare(b.name);
      return 0;
    });
  });

  // Shortlist helpers
  shortlistedSuppliers = computed(() => this.suppliers().filter(s => s.isShortlisted));

  toggleShortlist(id: string): void {
    const target = this.suppliers().find(s => s.id === id || this.matchesIdOrSlug(s, id));
    if (!target) return;

    const nextVal = !target.isShortlisted;

    // 1. Optimistic update in-memory and local storage
    this.suppliers.update(all =>
      all.map(s => {
        if (s.id === target.id) {
          this.saveShortlistState(s.id, nextVal);
          return { ...s, isShortlisted: nextVal };
        }
        return s;
      })
    );

    // 2. Persist to PostgreSQL backend via shortlists API
    this.http.get<any[]>(`${this.apiUrl}/shortlists`).subscribe({
      next: (cols) => {
        const defaultCol = Array.isArray(cols) && cols.length > 0 ? cols[0] : null;
        if (!defaultCol) return;

        if (nextVal) {
          this.http.post(`${this.apiUrl}/shortlists/${defaultCol.id}/items`, {
            businessId: target.id,
            notes: `Shortlisted from Enterprise Supplier Directory: ${target.name}`
          }).subscribe();
        } else {
          for (const col of (cols || [])) {
            if (col.items && col.items.some((i: any) => (i.businessId || '').toLowerCase() === target.id.toLowerCase())) {
              this.http.delete(`${this.apiUrl}/shortlists/${col.id}/items/${target.id}`).subscribe();
            }
          }
        }
      },
      error: () => {}
    });
  }

  // Compare selection helpers
  toggleCompare(id: string): void {
    this.selectedForCompare.update(curr => {
      if (curr.includes(id)) {
        return curr.filter(x => x !== id);
      } else {
        if (curr.length >= 4) return curr;
        return [...curr, id];
      }
    });
  }

  isComparing(id: string): boolean {
    return this.selectedForCompare().includes(id);
  }

  clearCompare(): void {
    this.selectedForCompare.set([]);
  }

  getAllSuppliers(): BuyerSupplier[] {
    return this.suppliers();
  }

  // Get supplier by ID (supports Guid or slug)
  getSupplierById(id: string): BuyerSupplier | undefined {
    const norm = id.toLowerCase().trim();
    return this.suppliers().find(s =>
      s.id.toLowerCase() === norm ||
      this.matchesIdOrSlug(s, norm) ||
      s.name.toLowerCase().includes(norm) ||
      s.legalName.toLowerCase().includes(norm)
    );
  }

  private matchesIdOrSlug(s: BuyerSupplier, query: string): boolean {
    const q = query.toLowerCase();
    return s.id.toLowerCase() === q || s.name.toLowerCase().includes(q) || s.legalName.toLowerCase().includes(q);
  }

  // Natural language query processor
  parseNaturalQuery(query: string): void {
    const q = query.trim().toLowerCase();
    this.searchQuery.set(query);

    let industry: string | undefined;
    let location: string | undefined;
    let cert: string | undefined;

    if (q.includes('electrical') || q.includes('component')) {
      industry = 'Electrical';
    } else if (q.includes('manufacturing') || q.includes('tooling') || q.includes('fastener')) {
      industry = 'Manufacturing';
    } else if (q.includes('logistics') || q.includes('transport') || q.includes('freight')) {
      industry = 'Logistics';
    } else if (q.includes('construction') || q.includes('steel')) {
      industry = 'Construction';
    } else if (q.includes('semiconductor') || q.includes('software') || q.includes('it')) {
      industry = 'IT Services';
    }

    if (q.includes('uttar pradesh') || q.includes('up') || q.includes('ghaziabad') || q.includes('noida') || q.includes('lucknow')) {
      location = 'Uttar Pradesh';
    } else if (q.includes('delhi') || q.includes('ncr') || q.includes('okhla')) {
      location = 'Delhi';
    } else if (q.includes('haryana') || q.includes('faridabad')) {
      location = 'Haryana';
    } else if (q.includes('maharashtra') || q.includes('mumbai') || q.includes('pune')) {
      location = 'Maharashtra';
    } else if (q.includes('karnataka') || q.includes('bengaluru') || q.includes('bangalore')) {
      location = 'Karnataka';
    }

    if (q.includes('iso')) {
      cert = 'ISO 9001';
    } else if (q.includes('bis')) {
      cert = 'BIS';
    } else if (q.includes('udyam') || q.includes('msme')) {
      cert = 'Udyam';
    }

    if (industry || location || cert) {
      this.parsedTokens.set({
        industry,
        location,
        certification: cert,
        active: true
      });

      this.filterState.update(curr => ({
        ...curr,
        industries: industry ? [industry] : curr.industries,
        state: location ? location : curr.state,
        certifications: cert ? [cert] : curr.certifications
      }));
    } else {
      this.parsedTokens.set({ active: false });
    }
  }

  setFilters(partial: Partial<SearchFilters>): void {
    this.filterState.update(curr => ({ ...curr, ...partial }));
  }

  resetFilters(): void {
    this.searchQuery.set('');
    this.parsedTokens.set({ active: false });
    this.activeStatusTab.set('all');
    this.filterState.set({
      industries: [],
      state: '',
      businessTypes: [],
      verificationStatus: 'all',
      minTrustScore: 0,
      certifications: [],
      complianceStanding: 'all',
      minYears: 0
    });
  }

  private isIdShortlisted(id: string): boolean {
    try {
      const stored = localStorage.getItem('veripass_buyer_shortlist');
      if (stored) {
        const ids: string[] = JSON.parse(stored);
        return ids.includes(id);
      }
    } catch {}
    return false;
  }

  private saveShortlistState(id: string, isShortlisted: boolean): void {
    try {
      let ids: string[] = [];
      const stored = localStorage.getItem('veripass_buyer_shortlist');
      if (stored) ids = JSON.parse(stored);
      if (isShortlisted) {
        if (!ids.includes(id)) ids.push(id);
      } else {
        ids = ids.filter(x => x !== id);
      }
      localStorage.setItem('veripass_buyer_shortlist', JSON.stringify(ids));
    } catch {}
  }

  private restoreShortlistFromStorage(): void {
    try {
      const stored = localStorage.getItem('veripass_buyer_shortlist');
      if (stored) {
        let ids: string[] = JSON.parse(stored);
        if (ids && ids.length) {
          // Remove legacy mock IDs
          ids = ids.filter(id => !id.startsWith('d1111111') && !id.startsWith('d2222222') && !id.startsWith('d3333333') && !id.startsWith('d4444444') && !id.startsWith('d5555555') && !id.startsWith('d6666666') && !id.startsWith('d7777777'));
          localStorage.setItem('veripass_buyer_shortlist', JSON.stringify(ids));
          this.suppliers.update(list =>
            list.map(s => (ids.includes(s.id) ? { ...s, isShortlisted: true } : s))
          );
        }
      }
    } catch {}
  }
}
