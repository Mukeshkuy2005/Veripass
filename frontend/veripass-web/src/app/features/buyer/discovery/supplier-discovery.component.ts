import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { BuyerSearchService } from '../../../core/services/buyer-search.service';

@Component({
  selector: 'app-supplier-discovery',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './supplier-discovery.component.html',
  styleUrl: './supplier-discovery.component.css'
})
export class SupplierDiscoveryComponent {
  private router = inject(Router);
  private searchService = inject(BuyerSearchService);

  searchQuery = '';

  filters = this.searchService.filterState;
  parsedTokens = this.searchService.parsedTokens;
  filteredCount = () => this.searchService.filteredSuppliers().length;

  onSearch(): void {
    if (this.searchQuery) {
      this.searchService.parseNaturalQuery(this.searchQuery);
    }
  }

  applyExampleSearch(query: string): void {
    this.searchQuery = query;
    this.searchService.parseNaturalQuery(query);
  }

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

  setStateFilter(state: string): void {
    this.searchService.setFilters({ state });
  }

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

  setVerificationStatus(status: string): void {
    this.searchService.setFilters({ verificationStatus: status });
  }

  setMinScore(score: number): void {
    this.searchService.setFilters({ minTrustScore: Number(score) });
  }

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

  toggleComplianceStanding(c: string): void {
    const curr = this.filters().complianceStanding;
    this.searchService.setFilters({ complianceStanding: curr === c ? 'all' : c });
  }

  setMinYears(years: number): void {
    this.searchService.setFilters({ minYears: Number(years) });
  }

  quickFilterCategory(cat: string): void {
    this.searchService.setFilters({ industries: [cat] });
    this.router.navigate(['/buyer/suppliers']);
  }

  applyAndNavigate(): void {
    this.router.navigate(['/buyer/suppliers']);
  }

  resetAllFilters(): void {
    this.searchQuery = '';
    this.searchService.resetFilters();
  }
}
