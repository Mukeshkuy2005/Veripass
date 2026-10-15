import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router, ActivatedRoute } from '@angular/router';
import { OnboardingService } from '../../../core/services/onboarding.service';
import { BuyerSearchService } from '../../../core/services/buyer-search.service';
import {
  OnboardingRequestSummary,
  OnboardingStatusCounts,
  OnboardingStatus,
  SupplierSearchItem,
  RequirementCatalogueItem
} from '../../../core/models/models';

@Component({
  selector: 'app-buyer-onboarding',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './buyer-onboarding.component.html',
  styleUrl: './buyer-onboarding.component.css'
})
export class BuyerOnboardingComponent implements OnInit {
  private onboardingService = inject(OnboardingService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  public searchService = inject(BuyerSearchService);

  // State Signals
  requests = signal<OnboardingRequestSummary[]>([]);
  totalCount = signal<number>(0);
  page = signal<number>(1);
  pageSize = signal<number>(12);
  loading = signal<boolean>(true);
  errorMsg = signal<string | null>(null);

  // Status Metrics
  statusCounts = signal<OnboardingStatusCounts>({
    newCount: 0,
    requestedCount: 0,
    inVerificationCount: 0,
    actionRequiredCount: 0,
    approvedCount: 0,
    rejectedCount: 0,
    expiredCount: 0,
    withdrawnCount: 0,
    totalActiveCount: 0
  });

  // UI Views and Filters
  viewMode = signal<'kanban' | 'table'>('kanban');
  searchQuery: string = '';
  selectedStatus = signal<string>('ALL');
  selectedCategory = signal<string>('ALL');
  sortBy = signal<string>('CreatedAt');
  sortDirection = signal<'asc' | 'desc'>('desc');

  // Start Onboarding Modal State
  isModalOpen = signal<boolean>(false);
  modalStep = signal<number>(1);
  supplierSearchQuery: string = '';
  supplierSearchResults = signal<SupplierSearchItem[]>([]);
  isSearchingSuppliers = signal<boolean>(false);
  selectedSupplier = signal<SupplierSearchItem | null>(null);

  // Requirements Catalogue
  requirementsCatalogue = signal<RequirementCatalogueItem[]>([]);
  selectedRequirements = signal<{ requirementCode: string; isMandatory: boolean }[]>([]);
  isLoadingRequirements = signal<boolean>(false);

  // Onboarding Form Model (standard properties to avoid Signal overwrite with ngModel)
  formPurpose: string = '';
  formCategory: string = 'Precision Engineering & Tooling';
  formDepartment: string = 'Engineering Procurement Division';
  formRelationship: string = 'Long-term OEM Partner';
  formPlant: string = '';
  formPaymentTerms: string = 'Net 45 Days';
  formExpiryDays: number = 30;
  formMessage: string = 'Please complete the statutory and technical verification to formalize vendor empanelment.';
  isSubmitting = signal<boolean>(false);
  submitSuccess = signal<string | null>(null);

  // Kanban Columns
  kanbanColumns = [
    { key: 'NEW', label: 'New', badgeClass: 'badge-new' },
    { key: 'REQUESTED', label: 'Requested', badgeClass: 'badge-requested' },
    { key: 'IN_VERIFICATION', label: 'In Verification', badgeClass: 'badge-verifying' },
    { key: 'ACTION_REQUIRED', label: 'Action Required', badgeClass: 'badge-action' },
    { key: 'APPROVED', label: 'Approved', badgeClass: 'badge-approved' },
    { key: 'REJECTED', label: 'Rejected', badgeClass: 'badge-rejected' }
  ];

  ngOnInit(): void {
    this.loadOnboardingData();
    this.route.queryParamMap.subscribe(params => {
      if (params.get('start') === 'true') {
        this.openStartModal();
      }
    });
  }

  loadOnboardingData(): void {
    this.loading.set(true);
    this.errorMsg.set(null);

    const params: any = {
      page: this.page(),
      pageSize: this.pageSize(),
      sortBy: this.sortBy(),
      sortDirection: this.sortDirection()
    };

    if (this.searchQuery.trim()) {
      params.search = this.searchQuery.trim();
    }

    if (this.selectedStatus() !== 'ALL') {
      params.status = this.selectedStatus();
    }

    if (this.selectedCategory() !== 'ALL') {
      params.category = this.selectedCategory();
    }

    this.onboardingService.getOnboardingRequests(params).subscribe({
      next: (res) => {
        this.requests.set(res.items || []);
        this.totalCount.set(res.totalCount || 0);
        if (res.statusCounts) {
          this.statusCounts.set(res.statusCounts);
        }
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load onboarding requests', err);
        this.errorMsg.set('Unable to retrieve onboarding requests from the compliance engine.');
        this.loading.set(false);
      }
    });
  }

  // Filter Actions
  onSearchChange(): void {
    this.page.set(1);
    this.loadOnboardingData();
  }

  setStatusFilter(status: string): void {
    this.selectedStatus.set(status);
    this.page.set(1);
    this.loadOnboardingData();
  }

  setCategoryFilter(category: string): void {
    this.selectedCategory.set(category);
    this.page.set(1);
    this.loadOnboardingData();
  }

  onSortChange(column: string): void {
    if (this.sortBy() === column) {
      this.sortDirection.set(this.sortDirection() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortBy.set(column);
      this.sortDirection.set('desc');
    }
    this.loadOnboardingData();
  }

  clearFilters(): void {
    this.searchQuery = '';
    this.selectedStatus.set('ALL');
    this.selectedCategory.set('ALL');
    this.page.set(1);
    this.loadOnboardingData();
  }

  // Pagination
  goToPage(newPage: number): void {
    if (newPage >= 1 && newPage <= this.totalPages()) {
      this.page.set(newPage);
      this.loadOnboardingData();
    }
  }

  totalPages = computed(() => {
    return Math.ceil(this.totalCount() / this.pageSize()) || 1;
  });

  // Kanban partition
  getRequestsByStatus(statusKey: string): OnboardingRequestSummary[] {
    return this.requests().filter(r => r.status === statusKey);
  }

  // Reset all wizard form fields
  resetForm(): void {
    this.modalStep.set(1);
    this.selectedSupplier.set(null);
    this.supplierSearchQuery = '';
    this.formPurpose = '';
    this.formCategory = 'Direct Material';
    this.formDepartment = 'Procurement';
    this.formRelationship = 'Tier 1 Strategic';
    this.formPlant = 'Main Manufacturing Facility';
    this.formPaymentTerms = 'Net 45 Days';
    this.formExpiryDays = 14;
    this.formMessage = 'Please complete sovereign onboarding and fulfill mandatory statutory credentials for vendor empanelment.';
    this.isSubmitting.set(false);
    this.submitSuccess.set(null);
    this.errorMsg.set(null);
  }

  // Open Start Onboarding Wizard
  openStartModal(): void {
    this.resetForm();
    this.isModalOpen.set(true);

    // Initial supplier search
    this.executeSupplierSearch();

    // Load requirements catalogue
    if (this.requirementsCatalogue().length === 0) {
      this.isLoadingRequirements.set(true);
      this.onboardingService.getRequirementsCatalogue().subscribe({
        next: (items) => {
          this.requirementsCatalogue.set(items);
          // Default select default mandatory requirements
          const defaults = items.map(i => ({
            requirementCode: i.code,
            isMandatory: i.isDefaultMandatory
          }));
          this.selectedRequirements.set(defaults);
          this.isLoadingRequirements.set(false);
        },
        error: (err) => {
          console.error('Failed to load requirements catalogue', err);
          this.isLoadingRequirements.set(false);
        }
      });
    }
  }

  closeStartModal(): void {
    this.isModalOpen.set(false);
    this.resetForm();
  }

  executeSupplierSearch(): void {
    this.isSearchingSuppliers.set(true);
    const query = this.supplierSearchQuery.trim().toLowerCase();

    this.onboardingService.searchSuppliers(query || undefined).subscribe({
      next: (results) => {
        // Enforce buyer supplier selection rule: only score > 50
        const suppliersList = (results || []).filter(s => (s.trustScore ?? 0) > 50);
        this.supplierSearchResults.set(suppliersList);
        this.isSearchingSuppliers.set(false);
      },
      error: (err) => {
        console.error('Error searching suppliers from backend:', err);
        this.supplierSearchResults.set([]);
        this.isSearchingSuppliers.set(false);
      }
    });
  }

  selectSupplier(supplier: SupplierSearchItem): void {
    this.selectedSupplier.set(supplier);
    if (!this.formPurpose) {
      this.formPurpose = `Empanelment & Procurement for ${supplier.businessName}`;
    }
  }

  nextModalStep(): void {
    this.errorMsg.set(null);
    if (this.modalStep() === 1 && !this.selectedSupplier()) {
      this.errorMsg.set('Please select a supplier to proceed to requirements definition.');
      return;
    }
    if (this.modalStep() === 2 && !this.formPurpose.trim()) {
      this.errorMsg.set('Please provide the business purpose for this onboarding request.');
      return;
    }
    if (this.modalStep() === 3 && this.selectedRequirements().length === 0) {
      this.errorMsg.set('Please select at least one verification requirement.');
      return;
    }
    if (this.modalStep() < 4) {
      this.modalStep.update(s => s + 1);
    }
  }

  prevModalStep(): void {
    this.errorMsg.set(null);
    if (this.modalStep() > 1) {
      this.modalStep.update(s => s - 1);
    }
  }

  toggleRequirement(code: string): void {
    const list = [...this.selectedRequirements()];
    const index = list.findIndex(r => r.requirementCode === code);
    if (index >= 0) {
      list.splice(index, 1);
    } else {
      const cat = this.requirementsCatalogue().find(c => c.code === code);
      list.push({ requirementCode: code, isMandatory: cat?.isDefaultMandatory ?? false });
    }
    this.selectedRequirements.set(list);
  }

  isRequirementSelected(code: string): boolean {
    return this.selectedRequirements().some(r => r.requirementCode === code);
  }

  toggleMandatory(code: string, event: Event): void {
    event.stopPropagation();
    const list = [...this.selectedRequirements()];
    const item = list.find(r => r.requirementCode === code);
    if (item) {
      item.isMandatory = !item.isMandatory;
      this.selectedRequirements.set(list);
    }
  }

  isMandatory(code: string): boolean {
    return this.selectedRequirements().find(r => r.requirementCode === code)?.isMandatory ?? false;
  }

  // Create Request
  submitOnboardingRequest(sendImmediately: boolean): void {
    if (this.isSubmitting()) return;

    const sup = this.selectedSupplier();
    if (!sup) {
      this.errorMsg.set('Please select a supplier to initiate onboarding.');
      return;
    }

    if (!this.formPurpose.trim()) {
      this.errorMsg.set('Please provide the business purpose for this onboarding request.');
      return;
    }

    if (this.selectedRequirements().length === 0) {
      this.errorMsg.set('Please select at least one verification requirement.');
      return;
    }

    // Duplicate check against current active onboarding requests
    const duplicate = this.requests().find(r =>
      r.supplierBusinessId === sup.id &&
      (r.status === 'NEW' || r.status === 'REQUESTED' || r.status === 'IN_VERIFICATION')
    );
    if (duplicate) {
      this.errorMsg.set(`An active onboarding workflow already exists for ${sup.businessName} (Status: ${duplicate.status}).`);
      return;
    }

    this.isSubmitting.set(true);
    this.errorMsg.set(null);

    const payload = {
      supplierBusinessId: sup.id,
      purpose: this.formPurpose.trim(),
      businessCategory: this.formCategory,
      procurementDepartment: this.formDepartment,
      expectedRelationship: this.formRelationship,
      message: this.formMessage.trim(),
      expiresAt: new Date(Date.now() + this.formExpiryDays * 24 * 60 * 60 * 1000).toISOString(),
      requirements: this.selectedRequirements(),
      sendImmediately
    };

    this.onboardingService.createOnboardingRequest(payload).subscribe({
      next: (res) => {
        this.isSubmitting.set(false);
        this.submitSuccess.set(res.message || 'Onboarding request successfully created.');
        this.searchService.loadSuppliersFromBackend();
        setTimeout(() => {
          this.closeStartModal();
          this.resetForm();
          this.loadOnboardingData();
        }, 1200);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        if (err.status === 401) {
          this.errorMsg.set('Your session has expired. Please log in again to complete onboarding.');
        } else if (err.status === 409 || (err.error?.message && err.error.message.toLowerCase().includes('already exist'))) {
          this.errorMsg.set(`An active onboarding workflow already exists for ${sup.businessName}.`);
        } else {
          this.errorMsg.set(err.error?.message || 'Failed to submit onboarding request. Please verify inputs and try again.');
        }
      }
    });
  }

  // Navigation
  viewDetail(id: string): void {
    this.router.navigate(['/buyer/onboarding', id]);
  }

  // Visual helper
  getStatusBadgeClass(status: string): string {
    switch (status) {
      case 'NEW': return 'badge-new';
      case 'REQUESTED': return 'badge-requested';
      case 'IN_VERIFICATION': return 'badge-verifying';
      case 'ACTION_REQUIRED': return 'badge-action';
      case 'APPROVED': return 'badge-approved';
      case 'REJECTED': return 'badge-rejected';
      case 'EXPIRED': return 'badge-expired';
      case 'WITHDRAWN': return 'badge-withdrawn';
      default: return 'badge-default';
    }
  }

  formatDate(dateStr?: string): string {
    if (!dateStr) return '—';
    const d = new Date(dateStr);
    return d.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' });
  }
}
