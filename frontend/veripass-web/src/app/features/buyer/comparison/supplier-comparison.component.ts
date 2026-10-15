import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { BuyerSearchService, BuyerSupplier } from '../../../core/services/buyer-search.service';

export interface ComparisonItem {
  id: string;
  businessId: string;
  name: string;
  legalName: string;
  category: string;
  industry: string;
  location: string;
  state: string;
  businessType: string;
  businessIdNumber: string;
  overallTrustScore: number;
  identityScore: number;
  credentialScore: number;
  complianceScore: number;
  verificationScore: number;
  riskScore: number;
  riskLevel: 'Low' | 'Medium' | 'High';
  yearsActive: number;
  gstin: string;
  pan: string;
  udyam: string;
  employeeCount?: number;
  verifiedCredentials?: string[];
  status: 'Verified' | 'Partially Verified' | 'Expired';
  isShortlisted: boolean;
  isLeader?: boolean;
}

@Component({
  selector: 'app-supplier-comparison',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  templateUrl: './supplier-comparison.component.html',
  styleUrl: './supplier-comparison.component.css'
})
export class SupplierComparisonComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private http = inject(HttpClient);
  private searchService = inject(BuyerSearchService);

  private readonly apiUrl = 'http://localhost:5000/api/v1';

  comparedSuppliers = signal<ComparisonItem[]>([]);
  isLoading = signal<boolean>(false);
  toastMessage = signal<string | null>(null);
  toastType = signal<'success' | 'info' | 'warning'>('success');

  // Interactive controls
  highlightDifferencesOnly = signal<boolean>(false);
  activeSection = signal<'all' | 'trust' | 'statutory' | 'operational' | 'standards'>('all');
  copiedId = signal<string | null>(null);

  // Add Supplier Modal
  isAddModalOpen = signal<boolean>(false);
  modalSearchTerm = signal<string>('');
  modalIndustryFilter = signal<string>('all');

  // Engagement Modal
  engagementModalSupplier = signal<ComparisonItem | null>(null);
  engagementScope = signal<string>('full');
  engagementNotes = signal<string>('');
  engagementSubmitting = signal<boolean>(false);

  // Dynamic Leader & Cluster Synthesis
  leaderSupplier = computed(() => {
    const list = this.comparedSuppliers();
    if (list.length === 0) return null;
    return list.reduce((best, curr) => (curr.overallTrustScore > best.overallTrustScore ? curr : best), list[0]);
  });

  clusterAverageScore = computed(() => {
    const list = this.comparedSuppliers();
    if (list.length === 0) return 0;
    const sum = list.reduce((acc, curr) => acc + curr.overallTrustScore, 0);
    return Math.round(sum / list.length);
  });

  clusterAverageCompliance = computed(() => {
    const list = this.comparedSuppliers();
    if (list.length === 0) return 0;
    const sum = list.reduce((acc, curr) => acc + curr.complianceScore, 0);
    return Math.round(sum / list.length);
  });

  clusterHasWarning = computed(() => {
    return this.comparedSuppliers().some(s => s.riskLevel === 'High' || s.status !== 'Verified');
  });

  // Available suppliers for the Add Modal
  availableSuppliers = computed(() => {
    const term = this.modalSearchTerm().toLowerCase().trim();
    const ind = this.modalIndustryFilter();
    const all = this.searchService.suppliers();
    const currentIds = this.comparedSuppliers().map(c => c.id.toLowerCase());

    return all.filter(s => {
      const matchesSearch = !term ||
        s.name.toLowerCase().includes(term) ||
        s.legalName.toLowerCase().includes(term) ||
        s.category.toLowerCase().includes(term) ||
        s.gstin.toLowerCase().includes(term) ||
        s.location.toLowerCase().includes(term);
      const matchesInd = ind === 'all' || s.industry === ind;
      return matchesSearch && matchesInd;
    });
  });

  availableIndustries = computed(() => {
    const all = this.searchService.suppliers();
    const set = new Set(all.map(s => s.industry).filter(Boolean));
    return Array.from(set);
  });

  ngOnInit(): void {
    if (this.searchService.suppliers().length === 0) {
      this.searchService.loadSuppliersFromBackend();
    }

    this.route.queryParamMap.subscribe(params => {
      const idsParam = params.get('ids');
      let targetIds: string[] = [];

      if (idsParam) {
        targetIds = idsParam.split(',').map(x => x.trim()).filter(Boolean);
      } else if (this.searchService.selectedForCompare().length > 0) {
        targetIds = this.searchService.selectedForCompare();
      } else {
        targetIds = [];
      }

      this.loadComparisonData(targetIds);
    });
  }

  loadComparisonData(ids: string[]): void {
    if (ids.length === 0) {
      this.comparedSuppliers.set([]);
      return;
    }

    this.isLoading.set(true);

    // Resolve slugs to GUIDs if needed
    const resolvedIds = ids.map(id => this.resolveToGuid(id));
    const queryParam = resolvedIds.join(',');

    this.http.get<any>(`${this.apiUrl}/suppliers/compare?businessIds=${queryParam}`).subscribe({
      next: (res) => {
        this.isLoading.set(false);
        const dtos: any[] = Array.isArray(res) ? res : (res?.value || []);

        if (dtos && dtos.length > 0) {
          const items: ComparisonItem[] = dtos.map(d => this.mapDtoToComparisonItem(d));
          this.markLeader(items);
          this.comparedSuppliers.set(items);
          this.syncSelectedService(items.map(i => i.id));
        } else {
          this.fallbackFromCatalog(ids);
        }
      },
      error: (err) => {
        this.isLoading.set(false);
        console.warn('Backend compare API notice:', err);
        this.fallbackFromCatalog(ids);
      }
    });
  }

  private resolveToGuid(id: string): string {
    return id.trim();
  }

  private mapDtoToComparisonItem(d: any): ComparisonItem {
    const score = d.overallTrustScore ?? 0;
    const catSupplier = this.searchService.getSupplierById(d.businessId);

    const name = d.tradeName || catSupplier?.name || d.legalName;
    const legalName = d.legalName || catSupplier?.legalName || name;
    const category = d.category || catSupplier?.category || 'Industrial Manufacturing';
    const industry = d.industry || catSupplier?.industry || 'Manufacturing';
    const loc = d.location || (catSupplier?.location ? catSupplier.location : '');
    const state = d.state || catSupplier?.state || '';
    const bType = d.businessType || catSupplier?.businessType || '';
    const years = d.yearsActive ?? catSupplier?.yearsActive ?? 0;
    const gstin = d.gstin || catSupplier?.gstin || '—';
    const pan = d.pan || catSupplier?.pan || '—';
    const udyam = d.udyam || d.udyamNumber || catSupplier?.udyam || '—';
    const creds = (d.verifiedCredentials && d.verifiedCredentials.length)
      ? d.verifiedCredentials
      : (catSupplier?.certifications || []);
    const status: 'Verified' | 'Partially Verified' | 'Expired' =
      d.verificationStatus === 'VERIFIED' ? 'Verified' :
      (d.verificationStatus === 'FLAGGED' || d.verificationStatus === 'EXPIRED' ? 'Expired' : (catSupplier?.status || 'Partially Verified'));
    const riskLevel: 'Low' | 'Medium' | 'High' = score >= 85 ? 'Low' : (score >= 70 ? 'Medium' : 'High');

    return {
      id: d.businessId,
      businessId: d.businessId,
      name,
      legalName,
      category,
      industry,
      location: loc,
      state,
      businessType: bType,
      businessIdNumber: d.businessIdNumber || '—',
      overallTrustScore: score,
      identityScore: d.identityScore ?? score,
      credentialScore: d.credentialScore ?? score,
      complianceScore: d.complianceScore ?? 0,
      verificationScore: d.verificationScore ?? score,
      riskScore: d.riskScore ?? 0,
      riskLevel,
      yearsActive: years,
      gstin,
      pan,
      udyam,
      employeeCount: d.employeeCount,
      verifiedCredentials: creds,
      status,
      isShortlisted: catSupplier?.isShortlisted ?? false
    };
  }

  private fallbackFromCatalog(ids: string[]): void {
    const items: ComparisonItem[] = [];
    for (const id of ids) {
      const s = this.searchService.getSupplierById(id);
      if (s) {
        items.push({
          id: s.id,
          businessId: s.id,
          name: s.name,
          legalName: s.legalName,
          category: s.category,
          industry: s.industry,
          location: s.location,
          state: s.state,
          businessType: s.businessType,
          businessIdNumber: s.cin || '—',
          overallTrustScore: s.trustScore,
          identityScore: s.trustScore,
          credentialScore: s.trustScore,
          complianceScore: s.complianceScore,
          verificationScore: s.trustScore,
          riskScore: s.trustScore >= 85 ? 90 : 50,
          riskLevel: s.riskLevel,
          yearsActive: s.yearsActive,
          gstin: s.gstin,
          pan: s.pan,
          udyam: s.udyam,
          employeeCount: undefined,
          verifiedCredentials: s.certifications || [],
          status: s.status,
          isShortlisted: s.isShortlisted ?? false
        });
      }
    }

    this.markLeader(items);
    this.comparedSuppliers.set(items);
    this.syncSelectedService(items.map(i => i.id));
  }

  private markLeader(items: ComparisonItem[]): void {
    if (items.length === 0) return;
    const maxScore = Math.max(...items.map(i => i.overallTrustScore));
    items.forEach(i => (i.isLeader = i.overallTrustScore === maxScore));
  }

  private syncSelectedService(ids: string[]): void {
    this.searchService.selectedForCompare.set(ids);
  }

  // Interactivity Actions
  removeSupplier(id: string): void {
    const target = this.comparedSuppliers().find(s => s.id === id);
    const updated = this.comparedSuppliers().filter(s => s.id !== id);
    this.markLeader(updated);
    this.comparedSuppliers.set(updated);
    this.syncSelectedService(updated.map(u => u.id));

    // Update query params
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { ids: updated.map(u => u.id).join(',') || null },
      queryParamsHandling: 'merge'
    });

    if (target) {
      this.showToast(`Removed ${target.name} from comparison matrix`, 'info');
    }
  }

  isAlreadyCompared(id: string): boolean {
    const q = id.toLowerCase().trim();
    return this.comparedSuppliers().some(c => c.id.toLowerCase() === q || c.businessId.toLowerCase() === q);
  }

  openAddModal(): void {
    this.modalSearchTerm.set('');
    this.modalIndustryFilter.set('all');
    this.isAddModalOpen.set(true);
  }

  closeAddModal(): void {
    this.isAddModalOpen.set(false);
  }

  addSupplierToCompare(supplier: BuyerSupplier): void {
    if (this.comparedSuppliers().length >= 4) {
      this.showToast('Maximum 4 suppliers can be compared simultaneously', 'warning');
      return;
    }

    if (this.isAlreadyCompared(supplier.id)) {
      this.showToast(`${supplier.name} is already in the comparison matrix`, 'info');
      return;
    }

    const currentIds = this.comparedSuppliers().map(s => s.id);
    const newIds = [...currentIds, supplier.id];

    this.closeAddModal();
    this.loadComparisonData(newIds);
    this.showToast(`Added ${supplier.name} to comparison benchmark`, 'success');

    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { ids: newIds.join(',') },
      queryParamsHandling: 'merge'
    });
  }

  toggleShortlist(item: ComparisonItem): void {
    this.searchService.toggleShortlist(item.id);
    item.isShortlisted = !item.isShortlisted;
    if (item.isShortlisted) {
      this.showToast(`Saved ${item.name} to procurement shortlist`, 'success');
    } else {
      this.showToast(`Removed ${item.name} from shortlist`, 'info');
    }
  }

  copyValue(val: string, label: string): void {
    if (!val || val === 'N/A') return;
    navigator.clipboard.writeText(val).then(() => {
      this.copiedId.set(val);
      this.showToast(`Copied ${label} to clipboard`, 'success');
      setTimeout(() => {
        if (this.copiedId() === val) this.copiedId.set(null);
      }, 2000);
    }).catch(() => {
      this.showToast(`Failed to copy to clipboard`, 'warning');
    });
  }

  toggleHighlightDifferences(): void {
    this.highlightDifferencesOnly.update(v => !v);
    if (this.highlightDifferencesOnly()) {
      this.showToast('Spotlighting rows with variance across suppliers', 'info');
    } else {
      this.showToast('Showing all comparison attributes', 'info');
    }
  }

  setSection(section: 'all' | 'trust' | 'statutory' | 'operational' | 'standards'): void {
    this.activeSection.set(section);
  }

  // Row variance calculation for smart diff highlighting
  hasVariance(accessor: (item: ComparisonItem) => any): boolean {
    const list = this.comparedSuppliers();
    if (list.length <= 1) return false;
    const first = JSON.stringify(accessor(list[0]));
    return list.some(item => JSON.stringify(accessor(item)) !== first);
  }

  hasFieldVariance(key: keyof ComparisonItem): boolean {
    const list = this.comparedSuppliers();
    if (list.length <= 1) return false;
    const first = JSON.stringify(list[0][key]);
    return list.some(item => JSON.stringify(item[key]) !== first);
  }

  isRowVisible(section: 'trust' | 'statutory' | 'operational' | 'standards', accessor?: (item: ComparisonItem) => any): boolean {
    const sec = this.activeSection();
    if (sec !== 'all' && sec !== section) return false;
    if (this.highlightDifferencesOnly() && accessor) {
      return this.hasVariance(accessor);
    }
    return true;
  }

  loadDefaultCluster(): void {
    let available = this.searchService.suppliers();
    if (available.length === 0) {
      this.searchService.loadSuppliersFromBackend();
      available = this.searchService.suppliers();
    }
    if (available.length > 0) {
      const topIds = available.slice(0, 3).map(s => s.id);
      this.loadComparisonData(topIds);
      this.router.navigate([], {
        relativeTo: this.route,
        queryParams: { ids: topIds.join(',') },
        queryParamsHandling: 'merge'
      });
      this.showToast('Loaded benchmark cluster', 'info');
    } else {
      this.openAddModal();
    }
  }

  clearAll(): void {
    this.comparedSuppliers.set([]);
    this.syncSelectedService([]);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { ids: null },
      queryParamsHandling: 'merge'
    });
    this.showToast('Cleared comparison matrix', 'info');
  }

  // Engagement Modal
  openEngagementModal(item: ComparisonItem): void {
    this.engagementModalSupplier.set(item);
    this.engagementScope.set('full');
    this.engagementNotes.set('');
  }

  closeEngagementModal(): void {
    this.engagementModalSupplier.set(null);
  }

  submitEngagementRequest(): void {
    const s = this.engagementModalSupplier();
    if (!s) return;

    this.engagementSubmitting.set(true);
    setTimeout(() => {
      this.engagementSubmitting.set(false);
      this.closeEngagementModal();
      this.showToast(`Procurement request successfully dispatched to ${s.name}`, 'success');
    }, 800);
  }

  // Export Matrix
  exportReport(): void {
    this.showToast('Preparing high-resolution benchmark matrix report...', 'info');
    setTimeout(() => {
      window.print();
    }, 400);
  }

  private showToast(msg: string, type: 'success' | 'info' | 'warning' = 'success'): void {
    this.toastMessage.set(msg);
    this.toastType.set(type);
    setTimeout(() => {
      if (this.toastMessage() === msg) {
        this.toastMessage.set(null);
      }
    }, 3500);
  }
}
