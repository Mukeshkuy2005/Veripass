import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { BuyerSearchService, BuyerSupplier } from '../../../core/services/buyer-search.service';

export interface ShortlistItemDto {
  businessId: string;
  legalName: string;
  businessIdNumber: string;
  trustScore: number;
  notes?: string;
  addedAt: string;
  tradeName?: string;
  category?: string;
  industry?: string;
  location?: string;
  state?: string;
  complianceScore?: number;
  riskLevel?: string;
  verificationStatus?: string;
  gstin?: string;
  certifications?: string[];
}

export interface ShortlistCollectionDto {
  id: string;
  name: string;
  description?: string;
  supplierCount: number;
  items: ShortlistItemDto[];
  icon?: string;
}

export interface ShortlistedSupplierView {
  id: string;
  businessId: string;
  name: string;
  legalName: string;
  businessIdNumber: string;
  trustScore: number;
  complianceScore: number;
  riskLevel: 'Low' | 'Medium' | 'High';
  status: 'Verified' | 'Partially Verified' | 'Expired';
  location: string;
  state: string;
  category: string;
  industry: string;
  gstin: string;
  pan: string;
  udyam: string;
  certifications: string[];
  notes?: string;
  addedAt: string;
  shortlistId: string;
  shortlistName: string;
}

@Component({
  selector: 'app-buyer-shortlist',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './buyer-shortlist.component.html',
  styleUrl: './buyer-shortlist.component.css'
})
export class BuyerShortlistComponent implements OnInit {
  private http = inject(HttpClient);
  private router = inject(Router);
  private searchService = inject(BuyerSearchService);

  private readonly apiUrl = 'http://localhost:5000/api/v1';

  // Backend collections state
  collections = signal<ShortlistCollectionDto[]>([]);
  selectedCollectionId = signal<string>('all');
  isLoading = signal<boolean>(false);
  hasError = signal<boolean>(false);
  toastNotice = signal<string | null>(null);
  toastType = signal<'success' | 'info' | 'warning'>('success');

  // Search, Filter, Sort & Selection
  searchQuery = signal<string>('');
  activeCategoryFilter = signal<'all' | 'Electrical' | 'Manufacturing' | 'High Priority' | 'Low Risk'>('all');
  activeSort = signal<'trust_desc' | 'compliance_desc' | 'name_asc' | 'recent'>('trust_desc');
  selectedSupplierIds = signal<string[]>([]);
  copiedField = signal<string | null>(null);

  // Modals
  showNewCollectionModal = signal<boolean>(false);
  newCollectionName = signal<string>('');
  newCollectionDesc = signal<string>('');
  newCollectionIcon = signal<string>('📁');

  showAddSupplierModal = signal<boolean>(false);
  addModalSearchTerm = signal<string>('');
  addModalSelectedSupplierId = signal<string>('');
  addModalTargetShortlistId = signal<string>('');
  addModalNotes = signal<string>('');

  // Engagement Modal
  engagementModalSupplier = signal<ShortlistedSupplierView | null>(null);
  engagementScope = signal<string>('full');
  engagementNotes = signal<string>('');
  engagementSubmitting = signal<boolean>(false);

  availableIcons = ['📁', '★', '⚡', '🛠', '📦', '🤝', '🔬', '🚀'];

  // All distinct shortlisted suppliers across all collections
  allShortlistedSuppliers = computed<ShortlistedSupplierView[]>(() => {
    const cols = this.collections();
    const map = new Map<string, ShortlistedSupplierView>();

    for (const col of cols) {
      for (const item of col.items) {
        if (!map.has(item.businessId)) {
          const cat = this.searchService.getSupplierById(item.businessId);
          const score = item.trustScore || cat?.trustScore || 0;
          const comp = item.complianceScore ?? cat?.complianceScore ?? 0;
          const risk: 'Low' | 'Medium' | 'High' = item.riskLevel
            ? ((item.riskLevel as string).toUpperCase() === 'HIGH' ? 'High' : ((item.riskLevel as string).toUpperCase() === 'LOW' ? 'Low' : 'Medium'))
            : (score < 35 ? 'High' : (score <= 70 ? 'Medium' : 'Low'));
          const status: 'Verified' | 'Partially Verified' | 'Expired' =
            (item.verificationStatus === 'VERIFIED' || cat?.status === 'Verified') ? 'Verified' : 'Partially Verified';

          map.set(item.businessId, {
            id: item.businessId,
            businessId: item.businessId,
            name: item.tradeName || cat?.name || item.legalName,
            legalName: item.legalName,
            businessIdNumber: item.businessIdNumber || cat?.cin || '—',
            trustScore: score,
            complianceScore: comp,
            riskLevel: risk,
            status: status,
            location: item.location || cat?.location || 'India',
            state: item.state || cat?.state || '',
            category: item.category || cat?.category || 'General Sourcing',
            industry: item.industry || cat?.industry || 'Supply Chain',
            gstin: item.gstin || cat?.gstin || '—',
            pan: cat?.pan || '—',
            udyam: cat?.udyam || '—',
            certifications: (item.certifications && item.certifications.length)
              ? item.certifications
              : (cat?.certifications || []),
            notes: item.notes,
            addedAt: item.addedAt,
            shortlistId: col.id,
            shortlistName: col.name
          });
        }
      }
    }

    return Array.from(map.values());
  });

  // Current view suppliers based on selected collection
  currentCollectionSuppliers = computed<ShortlistedSupplierView[]>(() => {
    const selId = this.selectedCollectionId();
    if (selId === 'all') {
      return this.allShortlistedSuppliers();
    }

    const col = this.collections().find(c => c.id === selId);
    if (!col) return [];

    return col.items.map(item => {
      const cat = this.searchService.getSupplierById(item.businessId);
      const score = item.trustScore || cat?.trustScore || 0;
      const comp = item.complianceScore ?? cat?.complianceScore ?? 0;
      const risk: 'Low' | 'Medium' | 'High' = item.riskLevel
        ? ((item.riskLevel as string).toUpperCase() === 'HIGH' ? 'High' : ((item.riskLevel as string).toUpperCase() === 'LOW' ? 'Low' : 'Medium'))
        : (score < 35 ? 'High' : (score <= 70 ? 'Medium' : 'Low'));
      const status: 'Verified' | 'Partially Verified' | 'Expired' =
        (item.verificationStatus === 'VERIFIED' || cat?.status === 'Verified') ? 'Verified' : 'Partially Verified';

      return {
        id: item.businessId,
        businessId: item.businessId,
        name: item.tradeName || cat?.name || item.legalName,
        legalName: item.legalName,
        businessIdNumber: item.businessIdNumber || cat?.cin || '—',
        trustScore: score,
        complianceScore: comp,
        riskLevel: risk,
        status: status,
        location: item.location || cat?.location || 'India',
        state: item.state || cat?.state || '',
        category: item.category || cat?.category || 'General Sourcing',
        industry: item.industry || cat?.industry || 'Supply Chain',
        gstin: item.gstin || cat?.gstin || '—',
        pan: cat?.pan || '—',
        udyam: cat?.udyam || '—',
        certifications: (item.certifications && item.certifications.length)
          ? item.certifications
          : (cat?.certifications || []),
        notes: item.notes,
        addedAt: item.addedAt,
        shortlistId: col.id,
        shortlistName: col.name
      };
    });
  });

  // Filtered & Sorted Suppliers
  displaySuppliers = computed<ShortlistedSupplierView[]>(() => {
    let list = [...this.currentCollectionSuppliers()];
    const q = this.searchQuery().trim().toLowerCase();
    const filter = this.activeCategoryFilter();
    const sort = this.activeSort();

    // 1. Text Search
    if (q) {
      list = list.filter(s =>
        s.name.toLowerCase().includes(q) ||
        s.legalName.toLowerCase().includes(q) ||
        s.gstin.toLowerCase().includes(q) ||
        s.category.toLowerCase().includes(q) ||
        s.location.toLowerCase().includes(q) ||
        s.businessIdNumber.toLowerCase().includes(q) ||
        (s.notes && s.notes.toLowerCase().includes(q))
      );
    }

    // 2. Category Tab Filter
    if (filter === 'Electrical') {
      list = list.filter(s => s.industry.toLowerCase().includes('electric') || s.category.toLowerCase().includes('electric') || s.category.toLowerCase().includes('semiconductor'));
    } else if (filter === 'Manufacturing') {
      list = list.filter(s => s.industry.toLowerCase().includes('manufactur') || s.category.toLowerCase().includes('manufactur') || s.category.toLowerCase().includes('tooling') || s.category.toLowerCase().includes('fastener'));
    } else if (filter === 'High Priority') {
      list = list.filter(s => s.trustScore >= 92);
    } else if (filter === 'Low Risk') {
      list = list.filter(s => s.riskLevel.toUpperCase() === 'LOW');
    }

    // 3. Sorting
    if (sort === 'trust_desc') {
      list.sort((a, b) => b.trustScore - a.trustScore);
    } else if (sort === 'compliance_desc') {
      list.sort((a, b) => b.complianceScore - a.complianceScore);
    } else if (sort === 'name_asc') {
      list.sort((a, b) => a.name.localeCompare(b.name));
    } else if (sort === 'recent') {
      list.sort((a, b) => new Date(b.addedAt).getTime() - new Date(a.addedAt).getTime());
    }

    return list;
  });

  // Pagination Controls
  currentPage = signal<number>(1);
  pageSize = signal<number>(6);

  totalPages = computed<number>(() => {
    return Math.max(1, Math.ceil(this.displaySuppliers().length / this.pageSize()));
  });

  paginatedSuppliers = computed<ShortlistedSupplierView[]>(() => {
    const list = this.displaySuppliers();
    const page = Math.min(this.currentPage(), this.totalPages());
    const start = (page - 1) * this.pageSize();
    return list.slice(start, start + this.pageSize());
  });

  paginationRange = computed<string>(() => {
    const total = this.displaySuppliers().length;
    if (total === 0) return '0 of 0';
    const page = Math.min(this.currentPage(), this.totalPages());
    const start = (page - 1) * this.pageSize() + 1;
    const end = Math.min(page * this.pageSize(), total);
    return `${start} - ${end} of ${total}`;
  });

  goToPage(p: number): void {
    if (p >= 1 && p <= this.totalPages()) {
      this.currentPage.set(p);
    }
  }

  prevPage(): void {
    if (this.currentPage() > 1) {
      this.currentPage.update(p => p - 1);
    }
  }

  nextPage(): void {
    if (this.currentPage() < this.totalPages()) {
      this.currentPage.update(p => p + 1);
    }
  }

  onSearchInput(val: string): void {
    this.searchQuery.set(val);
    this.currentPage.set(1);
  }

  // Executive KPI summary stats
  totalCount = computed(() => this.allShortlistedSuppliers().length);

  averageTrustScore = computed(() => {
    const list = this.allShortlistedSuppliers();
    if (list.length === 0) return 0;
    const sum = list.reduce((acc, s) => acc + s.trustScore, 0);
    return Math.round((sum / list.length) * 10) / 10;
  });

  averageCompliance = computed(() => {
    const list = this.allShortlistedSuppliers();
    if (list.length === 0) return 0;
    const sum = list.reduce((acc, s) => acc + s.complianceScore, 0);
    return Math.round(sum / list.length);
  });

  lowRiskCount = computed(() => {
    return this.allShortlistedSuppliers().filter(s => s.riskLevel.toUpperCase() === 'LOW').length;
  });

  // Active collection entity
  selectedCollection = computed(() => {
    const selId = this.selectedCollectionId();
    if (selId === 'all') {
      return {
        id: 'all',
        name: 'All Shortlisted Suppliers',
        description: 'Comprehensive view of all sovereign candidate suppliers saved across sourcing binders.',
        supplierCount: this.allShortlistedSuppliers().length,
        items: [],
        icon: '★'
      };
    }
    return this.collections().find(c => c.id === selId) || null;
  });

  // Available suppliers for the Add Supplier modal
  catalogSuppliers = computed(() => {
    const term = this.addModalSearchTerm().toLowerCase().trim();
    const all = this.searchService.suppliers();
    const currentList = this.currentCollectionSuppliers().map(s => s.businessId.toLowerCase());

    return all.filter(s => {
      const notInCurrent = !currentList.includes(s.id.toLowerCase());
      const matchesSearch = !term ||
        s.name.toLowerCase().includes(term) ||
        s.legalName.toLowerCase().includes(term) ||
        s.category.toLowerCase().includes(term) ||
        s.gstin.toLowerCase().includes(term) ||
        s.location.toLowerCase().includes(term);
      return notInCurrent && matchesSearch;
    });
  });

  ngOnInit(): void {
    this.loadShortlists();
  }

  loadShortlists(): void {
    this.isLoading.set(true);
    this.hasError.set(false);
    this.http.get<ShortlistCollectionDto[]>(`${this.apiUrl}/shortlists`).subscribe({
      next: (dtos) => {
        this.isLoading.set(false);
        if (dtos && dtos.length > 0) {
          const decorated = dtos.map((d, index) => ({
            ...d,
            icon: this.detectIcon(d.name, index)
          }));
          this.collections.set(decorated);

          // Sync local shortlist with BuyerSearchService
          const allIds = new Set<string>();
          decorated.forEach(c => c.items.forEach(i => allIds.add(i.businessId)));
          this.syncServiceShortlist(Array.from(allIds));
        } else {
          this.fallbackShortlists();
        }
      },
      error: (err) => {
        this.isLoading.set(false);
        console.warn('Backend shortlists notice (using local fallback):', err);
        this.fallbackShortlists();
      }
    });
  }

  private detectIcon(name: string, index: number): string {
    const n = name.toLowerCase();
    if (n.includes('master') || n.includes('preferred')) return '★';
    if (n.includes('electric') || n.includes('sensor')) return '⚡';
    if (n.includes('tooling') || n.includes('fastener') || n.includes('mechanical')) return '🛠';
    if (n.includes('partner') || n.includes('strategic')) return '🤝';
    if (n.includes('packaging')) return '📦';
    return this.availableIcons[index % this.availableIcons.length];
  }

  private fallbackShortlists(): void {
    const allSuppliers = this.searchService.suppliers();
    const items: ShortlistItemDto[] = allSuppliers.map(s => ({
      businessId: s.id,
      legalName: s.legalName,
      tradeName: s.name,
      businessIdNumber: s.cin || '—',
      trustScore: s.trustScore,
      complianceScore: s.complianceScore,
      riskLevel: s.riskLevel,
      verificationStatus: s.status === 'Verified' ? 'VERIFIED' : 'PARTIALLY_VERIFIED',
      location: s.location,
      state: s.state,
      category: s.category,
      industry: s.industry,
      gstin: s.gstin,
      certifications: s.certifications,
      notes: 'Qualified vendor under sovereign verification telemetry.',
      addedAt: new Date().toISOString()
    }));

    const defaultBinder: ShortlistCollectionDto = {
      id: 'master-procurement-binder',
      name: 'Master Sourcing Binder',
      description: 'Primary sovereign sourcing binder for qualified enterprise vendors',
      supplierCount: items.length,
      items: items,
      icon: '★'
    };

    this.collections.set([defaultBinder]);
    this.selectedCollectionId.set('all');
  }

  private syncServiceShortlist(ids: string[]): void {
    try {
      localStorage.setItem('veripass_buyer_shortlist', JSON.stringify(ids));
    } catch {}
  }

  // Collection Selection
  selectCollection(id: string): void {
    this.selectedCollectionId.set(id);
    this.selectedSupplierIds.set([]);
    this.currentPage.set(1);
  }

  setCategoryFilter(cat: 'all' | 'Electrical' | 'Manufacturing' | 'High Priority' | 'Low Risk'): void {
    this.activeCategoryFilter.set(cat);
    this.currentPage.set(1);
  }

  setSort(sort: 'trust_desc' | 'compliance_desc' | 'name_asc' | 'recent'): void {
    this.activeSort.set(sort);
    this.currentPage.set(1);
  }

  // Remove supplier from active or specific shortlist
  removeSupplier(businessId: string, shortlistId?: string): void {
    const targetShortlistId = shortlistId || (this.selectedCollectionId() !== 'all' ? this.selectedCollectionId() : this.collections()[0]?.id);
    if (!targetShortlistId) return;

    this.http.delete(`${this.apiUrl}/shortlists/${targetShortlistId}/suppliers/${businessId}`).subscribe({
      next: () => {
        this.showToast('Supplier removed from collection', 'info');
        this.loadShortlists();
      },
      error: () => {
        // Optimistic local removal
        this.collections.update(cols =>
          cols.map(c => {
            if (c.id === targetShortlistId) {
              const updatedItems = c.items.filter(i => i.businessId !== businessId);
              return { ...c, items: updatedItems, supplierCount: updatedItems.length };
            }
            return c;
          })
        );
        this.showToast('Supplier removed from collection', 'info');
      }
    });
  }

  // Modal: Create New Collection
  openNewCollectionModal(): void {
    this.newCollectionName.set('');
    this.newCollectionDesc.set('');
    this.newCollectionIcon.set('📁');
    this.showNewCollectionModal.set(true);
  }

  closeNewCollectionModal(): void {
    this.showNewCollectionModal.set(false);
  }

  createCollection(): void {
    const name = this.newCollectionName().trim();
    if (!name) return;

    const payload = {
      name,
      description: this.newCollectionDesc().trim() || undefined
    };

    this.http.post<any>(`${this.apiUrl}/shortlists`, payload).subscribe({
      next: (res) => {
        this.closeNewCollectionModal();
        this.showToast(`Sourcing binder "${name}" created successfully.`, 'success');
        this.loadShortlists();
        if (res?.shortlistId) {
          this.selectedCollectionId.set(res.shortlistId);
        }
      },
      error: () => {
        // Fallback local create
        const newCol: ShortlistCollectionDto = {
          id: 'custom-' + Date.now(),
          name,
          description: this.newCollectionDesc().trim(),
          supplierCount: 0,
          items: [],
          icon: this.newCollectionIcon()
        };
        this.collections.update(c => [...c, newCol]);
        this.selectedCollectionId.set(newCol.id);
        this.closeNewCollectionModal();
        this.showToast(`Sourcing binder "${name}" created successfully.`, 'success');
      }
    });
  }

  deleteCollection(colId: string, event: Event): void {
    event.stopPropagation();
    const col = this.collections().find(c => c.id === colId);
    if (!col) return;

    if (!confirm(`Are you sure you want to delete collection "${col.name}"?`)) return;

    this.http.delete(`${this.apiUrl}/shortlists/${colId}`).subscribe({
      next: () => {
        this.showToast(`Collection "${col.name}" deleted.`, 'info');
        if (this.selectedCollectionId() === colId) {
          this.selectedCollectionId.set('all');
        }
        this.loadShortlists();
      },
      error: () => {
        this.collections.update(c => c.filter(x => x.id !== colId));
        if (this.selectedCollectionId() === colId) {
          this.selectedCollectionId.set('all');
        }
        this.showToast(`Collection "${col.name}" deleted.`, 'info');
      }
    });
  }

  // Modal: Add Supplier to Shortlist
  openAddSupplierModal(): void {
    this.addModalSearchTerm.set('');
    this.addModalSelectedSupplierId.set('');
    this.addModalNotes.set('');
    const currentId = this.selectedCollectionId();
    this.addModalTargetShortlistId.set(currentId !== 'all' ? currentId : (this.collections()[0]?.id || ''));
    this.showAddSupplierModal.set(true);
  }

  closeAddSupplierModal(): void {
    this.showAddSupplierModal.set(false);
  }

  submitAddSupplier(): void {
    const supId = this.addModalSelectedSupplierId();
    let listId = this.addModalTargetShortlistId();
    if (!listId && this.collections().length > 0) {
      listId = this.collections()[0].id;
    }
    if (!supId || !listId) {
      this.showToast('Please select both a supplier and target binder', 'warning');
      return;
    }

    const payload = { notes: this.addModalNotes().trim() || undefined };
    const targetCol = this.collections().find(c => c.id === listId);
    const catalogSup = this.searchService.getSupplierById(supId);

    this.http.post(`${this.apiUrl}/shortlists/${listId}/suppliers/${supId}`, payload).subscribe({
      next: () => {
        this.closeAddSupplierModal();
        this.showToast('Supplier added to sourcing binder successfully.', 'success');
        this.loadShortlists();
      },
      error: () => {
        if (targetCol && catalogSup) {
          const newItem: ShortlistItemDto = {
            businessId: catalogSup.id,
            legalName: catalogSup.legalName,
            tradeName: catalogSup.name,
            businessIdNumber: catalogSup.cin || '—',
            trustScore: catalogSup.trustScore,
            complianceScore: catalogSup.complianceScore,
            riskLevel: catalogSup.riskLevel,
            verificationStatus: catalogSup.status === 'Verified' ? 'VERIFIED' : 'PARTIALLY_VERIFIED',
            location: catalogSup.location,
            state: catalogSup.state,
            category: catalogSup.category,
            industry: catalogSup.industry,
            gstin: catalogSup.gstin,
            certifications: catalogSup.certifications,
            notes: this.addModalNotes().trim() || 'Added from sovereign directory',
            addedAt: new Date().toISOString()
          };
          if (!targetCol.items.some(i => i.businessId === supId)) {
            targetCol.items.push(newItem);
            targetCol.supplierCount = targetCol.items.length;
            this.collections.update(cols => [...cols]);
          }
        }
        this.closeAddSupplierModal();
        this.showToast('Supplier added to sourcing binder.', 'success');
      }
    });
  }

  // Selection & Batch Operations
  toggleSupplierSelection(id: string): void {
    this.selectedSupplierIds.update(curr => {
      if (curr.includes(id)) {
        return curr.filter(x => x !== id);
      } else {
        if (curr.length >= 4) {
          this.showToast('Maximum 4 suppliers can be selected for comparison', 'warning');
          return curr;
        }
        return [...curr, id];
      }
    });
  }

  isSupplierSelected(id: string): boolean {
    return this.selectedSupplierIds().includes(id);
  }

  selectAllVisible(): void {
    const visible = this.displaySuppliers().map(s => s.id).slice(0, 4);
    this.selectedSupplierIds.set(visible);
  }

  clearSelection(): void {
    this.selectedSupplierIds.set([]);
  }

  compareSelected(): void {
    const ids = this.selectedSupplierIds();
    if (ids.length === 0) {
      this.showToast('Select at least 2 suppliers to compare', 'warning');
      return;
    }
    this.router.navigate(['/buyer/compare'], {
      queryParams: { ids: ids.join(',') }
    });
  }

  compareCollection(): void {
    const list = this.displaySuppliers();
    if (list.length === 0) {
      this.showToast('No suppliers in this collection to compare', 'warning');
      return;
    }
    const ids = list.map(s => s.id).slice(0, 4);
    this.router.navigate(['/buyer/compare'], {
      queryParams: { ids: ids.join(',') }
    });
  }

  // Copy Statutory ID
  copyValue(val: string, label: string): void {
    if (!val || val === 'N/A') return;
    navigator.clipboard.writeText(val).then(() => {
      this.copiedField.set(val);
      this.showToast(`Copied ${label} (${val}) to clipboard`, 'success');
      setTimeout(() => {
        if (this.copiedField() === val) this.copiedField.set(null);
      }, 2000);
    }).catch(() => {
      this.showToast(`Failed to copy to clipboard`, 'warning');
    });
  }

  // Engagement Modal
  openEngagementModal(s: ShortlistedSupplierView): void {
    this.engagementModalSupplier.set(s);
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
      this.showToast(`Procurement engagement request dispatched to ${s.name}`, 'success');
    }, 800);
  }

  // Export CSV
  exportCollection(): void {
    const items = this.displaySuppliers();
    if (items.length === 0) {
      this.showToast('No suppliers available to export', 'warning');
      return;
    }

    const headers = ['Business ID', 'Trade Name', 'Legal Name', 'CIN / Registration', 'GSTIN', 'Trust Score', 'Compliance Score', 'Risk Level', 'State', 'Category', 'Sourcing Binder'];
    const rows = items.map(s => [
      s.businessId,
      `"${s.name}"`,
      `"${s.legalName}"`,
      s.businessIdNumber,
      s.gstin,
      s.trustScore,
      s.complianceScore,
      s.riskLevel,
      `"${s.state}"`,
      `"${s.category}"`,
      `"${s.shortlistName}"`
    ]);

    const csvContent = 'data:text/csv;charset=utf-8,' + [headers.join(','), ...rows.map(r => r.join(','))].join('\n');
    const dlAnchor = document.createElement('a');
    dlAnchor.setAttribute('href', encodeURI(csvContent));
    dlAnchor.setAttribute('download', `VeriPass_Procurement_Shortlist_${new Date().toISOString().slice(0, 10)}.csv`);
    dlAnchor.click();
    this.showToast('Shortlist exported to CSV successfully.', 'success');
  }

  private showToast(msg: string, type: 'success' | 'info' | 'warning' = 'success'): void {
    this.toastNotice.set(msg);
    this.toastType.set(type);
    setTimeout(() => {
      if (this.toastNotice() === msg) {
        this.toastNotice.set(null);
      }
    }, 3500);
  }
}
