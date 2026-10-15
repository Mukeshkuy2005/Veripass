import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule, ActivatedRoute } from '@angular/router';
import { BuyerSearchService, BuyerSupplier } from '../../../core/services/buyer-search.service';

@Component({
  selector: 'app-supplier-search-results',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './supplier-search-results.component.html',
  styleUrl: './supplier-search-results.component.css'
})
export class SupplierSearchResultsComponent implements OnInit {
  private searchService = inject(BuyerSearchService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  // View state
  viewMode = signal<'cards' | 'table'>('cards');
  filterSidebarOpen = signal<boolean>(true);
  toastMessage = signal<string | null>(null);

  // Engagement modal state
  selectedSupplierForEngagement = signal<BuyerSupplier | null>(null);
  engagementScope = signal<string>('full');
  engagementNotes = signal<string>('');
  engagementSubmitting = signal<boolean>(false);

  // Search and filter signals
  suppliers = this.searchService.filteredSuppliers;
  filters = this.searchService.filterState;
  query = this.searchService.searchQuery;
  parsedTokens = this.searchService.parsedTokens;
  currentSort = this.searchService.selectedSort;
  activeTab = this.searchService.activeStatusTab;
  selectedForCompare = this.searchService.selectedForCompare;

  // Local model for search input to allow smooth typing
  searchInputValue = '';

  ngOnInit(): void {
    // Listen for query params from dashboard or layout search
    this.route.queryParams.subscribe(params => {
      if (params['q']) {
        this.searchInputValue = params['q'];
        this.searchService.parseNaturalQuery(params['q']);
      }
      if (params['status']) {
        const s = params['status'].toUpperCase();
        if (s === 'APPROVED' || s === 'VERIFIED') {
          this.searchService.activeStatusTab.set('verified');
        } else if (s === 'PENDING' || s === 'IN_PROGRESS') {
          this.searchService.activeStatusTab.set('partially_verified');
        }
      }
      if (params['risk'] && params['risk'].toUpperCase() === 'HIGH') {
        this.searchService.activeStatusTab.set('flagged');
      }
    });

    this.searchInputValue = this.query();
  }

  // Search actions
  onSearch(): void {
    const val = this.searchInputValue.trim();
    this.searchService.parseNaturalQuery(val);
  }

  clearSearchInput(): void {
    this.searchInputValue = '';
    this.searchService.searchQuery.set('');
    this.searchService.parsedTokens.set({ active: false });
  }

  applyExampleSearch(queryText: string): void {
    this.searchInputValue = queryText;
    this.searchService.parseNaturalQuery(queryText);
    this.showToast(`Applied intelligent filter: "${queryText}"`);
  }

  // Tab switcher
  setActiveTab(tab: 'all' | 'verified' | 'partially_verified' | 'shortlisted' | 'flagged'): void {
    this.searchService.activeStatusTab.set(tab);
  }

  // Sidebar controls
  toggleSidebar(): void {
    this.filterSidebarOpen.update(v => !v);
  }

  setViewMode(mode: 'cards' | 'table'): void {
    this.viewMode.set(mode);
  }

  onSortChange(sort: string): void {
    this.searchService.selectedSort.set(sort);
  }

  // Industry filters
  isIndustrySelected(ind: string): boolean {
    return this.filters().industries.includes(ind);
  }

  toggleIndustry(ind: string): void {
    const list = [...this.filters().industries];
    const idx = list.indexOf(ind);
    if (idx >= 0) list.splice(idx, 1);
    else list.push(ind);
    this.searchService.setFilters({ industries: list });
  }

  removeIndustry(ind: string): void {
    const list = this.filters().industries.filter(i => i !== ind);
    this.searchService.setFilters({ industries: list });
  }

  // State / Location
  setStateFilter(state: string): void {
    this.searchService.setFilters({ state });
  }

  clearState(): void {
    this.searchService.setFilters({ state: '' });
  }

  // Business Type
  isBusinessTypeSelected(bt: string): boolean {
    return this.filters().businessTypes.includes(bt);
  }

  toggleBusinessType(bt: string): void {
    const list = [...this.filters().businessTypes];
    const idx = list.indexOf(bt);
    if (idx >= 0) list.splice(idx, 1);
    else list.push(bt);
    this.searchService.setFilters({ businessTypes: list });
  }

  // Trust score threshold
  setMinScore(score: any): void {
    this.searchService.setFilters({ minTrustScore: Number(score) });
  }

  // Certifications
  isCertSelected(c: string): boolean {
    return this.filters().certifications.includes(c);
  }

  toggleCert(c: string): void {
    const list = [...this.filters().certifications];
    const idx = list.indexOf(c);
    if (idx >= 0) list.splice(idx, 1);
    else list.push(c);
    this.searchService.setFilters({ certifications: list });
  }

  // Compliance Standing
  toggleComplianceStanding(c: string): void {
    const curr = this.filters().complianceStanding;
    this.searchService.setFilters({ complianceStanding: curr === c ? 'all' : c });
  }

  // Operating vintage
  setMinYears(years: any): void {
    this.searchService.setFilters({ minYears: Number(years) });
  }

  // Reset
  resetAllFilters(): void {
    this.searchInputValue = '';
    this.searchService.resetFilters();
    this.showToast('All search filters reset');
  }

  // Shortlist
  toggleShortlist(s: BuyerSupplier): void {
    this.searchService.toggleShortlist(s.id);
    const updated = this.searchService.getSupplierById(s.id);
    if (updated?.isShortlisted) {
      this.showToast(`Added ${s.name} to Shortlist`);
    } else {
      this.showToast(`Removed ${s.name} from Shortlist`);
    }
  }

  // Compare selection
  isComparing(id: string): boolean {
    return this.searchService.isComparing(id);
  }

  toggleCompare(id: string, name: string): void {
    const isCurrentlyComparing = this.isComparing(id);
    if (!isCurrentlyComparing && this.selectedForCompare().length >= 4) {
      this.showToast('Maximum 4 suppliers can be compared simultaneously');
      return;
    }
    this.searchService.toggleCompare(id);
    if (!isCurrentlyComparing) {
      this.showToast(`Added ${name} to Comparison Tray`);
    }
  }

  clearCompare(): void {
    this.searchService.clearCompare();
    this.showToast('Comparison tray cleared');
  }

  getComparedSupplierObjects(): BuyerSupplier[] {
    return this.selectedForCompare()
      .map(id => this.searchService.getSupplierById(id))
      .filter((s): s is BuyerSupplier => s !== undefined);
  }

  navigateToCompare(): void {
    this.router.navigate(['/buyer/compare']);
  }

  // Engagement Modal
  openEngagementModal(s: BuyerSupplier): void {
    this.selectedSupplierForEngagement.set(s);
    this.engagementScope.set('full');
    this.engagementNotes.set('');
  }

  closeEngagementModal(): void {
    this.selectedSupplierForEngagement.set(null);
  }

  submitEngagementRequest(): void {
    const s = this.selectedSupplierForEngagement();
    if (!s) return;

    this.engagementSubmitting.set(true);
    setTimeout(() => {
      this.engagementSubmitting.set(false);
      this.closeEngagementModal();
      this.showToast(`Verification and engagement request dispatched to ${s.name}`);
    }, 800);
  }

  // Save Query
  saveSearch(): void {
    this.showToast('Search criteria saved to Enterprise Procurement Library');
  }

  // Helpers
  hasActiveFilters(): boolean {
    const f = this.filters();
    return !!(
      this.query() ||
      f.industries.length > 0 ||
      f.state ||
      f.businessTypes.length > 0 ||
      f.certifications.length > 0 ||
      f.minTrustScore > 70 ||
      f.complianceStanding !== 'all' ||
      f.minYears > 0 ||
      this.activeTab() !== 'all'
    );
  }

  private showToast(msg: string): void {
    this.toastMessage.set(msg);
    setTimeout(() => {
      if (this.toastMessage() === msg) {
        this.toastMessage.set(null);
      }
    }, 3500);
  }
}
