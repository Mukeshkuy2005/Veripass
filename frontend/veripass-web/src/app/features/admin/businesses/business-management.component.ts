import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AdminStateService, AdminBusiness } from '../../../core/services/admin-state.service';

@Component({
  selector: 'app-business-management',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './business-management.component.html',
  styleUrl: './business-management.component.css'
})
export class BusinessManagementComponent {
  private adminState = inject(AdminStateService);

  searchQuery = '';
  statusFilter = 'ALL';
  riskFilter = 'ALL';
  industryFilter = 'ALL';
  stateFilter = 'ALL';

  selectedBiz = signal<AdminBusiness | null>(null);
  targetBiz = signal<AdminBusiness | null>(null);
  modalMode = signal<'NONE' | 'SUSPEND' | 'FLAG'>('NONE');

  actionReason = '';
  customPassportId = '';
  attemptedSubmit = false;

  businesses = this.adminState.businesses;

  filteredBusinesses = computed(() => {
    let list = this.businesses();
    const q = this.searchQuery.trim().toLowerCase();

    if (q) {
      list = list.filter(b => 
        b.businessName.toLowerCase().includes(q) ||
        b.legalName.toLowerCase().includes(q) ||
        b.gstin.toLowerCase().includes(q) ||
        b.cin.toLowerCase().includes(q) ||
        b.passportId.toLowerCase().includes(q)
      );
    }

    if (this.statusFilter !== 'ALL') {
      list = list.filter(b => b.status === this.statusFilter);
    }

    if (this.riskFilter !== 'ALL') {
      list = list.filter(b => b.riskRating === this.riskFilter);
    }

    if (this.industryFilter !== 'ALL') {
      list = list.filter(b => b.industry.includes(this.industryFilter));
    }

    if (this.stateFilter !== 'ALL') {
      list = list.filter(b => b.state.includes(this.stateFilter));
    }

    return list;
  });

  selectBusiness(b: AdminBusiness): void {
    this.selectedBiz.set(b);
  }

  closeDrawer(): void {
    this.selectedBiz.set(null);
  }

  openSuspendModal(b: AdminBusiness): void {
    this.targetBiz.set(b);
    this.modalMode.set('SUSPEND');
    this.actionReason = '';
    this.attemptedSubmit = false;
  }

  openFlagModal(b: AdminBusiness | null): void {
    this.targetBiz.set(b);
    this.customPassportId = b ? b.passportId : '';
    this.modalMode.set('FLAG');
    this.actionReason = '';
    this.attemptedSubmit = false;
  }

  closeModal(): void {
    this.modalMode.set('NONE');
    this.targetBiz.set(null);
  }

  confirmSuspend(passportId: string): void {
    this.attemptedSubmit = true;
    if (!this.actionReason.trim()) return;

    this.adminState.suspendBusiness(passportId, this.actionReason);
    this.closeModal();
    if (this.selectedBiz()?.passportId === passportId) {
      this.selectedBiz.update(b => b ? { ...b, status: 'SUSPENDED', suspensionReason: this.actionReason } : null);
    }
  }

  confirmFlag(): void {
    this.attemptedSubmit = true;
    const target = this.targetBiz()?.passportId || this.customPassportId.trim();
    if (!target || !this.actionReason.trim()) return;

    this.adminState.flagBusiness(target, this.actionReason);
    this.closeModal();
    if (this.selectedBiz()?.passportId === target) {
      this.selectedBiz.update(b => b ? { ...b, status: 'FLAGGED', flagReason: this.actionReason } : null);
    }
  }

  reactivate(passportId: string): void {
    this.adminState.reactivateBusiness(passportId);
    if (this.selectedBiz()?.passportId === passportId) {
      this.selectedBiz.update(b => b ? { ...b, status: 'ACTIVE', suspensionReason: undefined, flagReason: undefined } : null);
    }
  }

  resetFilters(): void {
    this.searchQuery = '';
    this.statusFilter = 'ALL';
    this.riskFilter = 'ALL';
    this.industryFilter = 'ALL';
    this.stateFilter = 'ALL';
  }

  exportRegistry(): void {
    // Generates CSV export
  }
}
