import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AdminStateService, AdminBuyer } from '../../../core/services/admin-state.service';

@Component({
  selector: 'app-buyer-management',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './buyer-management.component.html',
  styleUrl: './buyer-management.component.css'
})
export class BuyerManagementComponent {
  private adminState = inject(AdminStateService);

  searchQuery = '';
  statusFilter = 'ALL';

  selectedBuyer = signal<AdminBuyer | null>(null);
  suspendTarget = signal<AdminBuyer | null>(null);
  suspendReason = '';

  buyers = this.adminState.buyers;

  filteredBuyers = computed(() => {
    let list = this.buyers();
    const q = this.searchQuery.trim().toLowerCase();

    if (q) {
      list = list.filter(b => 
        b.organizationName.toLowerCase().includes(q) ||
        b.orgId.toLowerCase().includes(q) ||
        b.industry.toLowerCase().includes(q)
      );
    }

    if (this.statusFilter !== 'ALL') {
      list = list.filter(b => b.status === this.statusFilter);
    }

    return list;
  });

  selectBuyer(b: AdminBuyer): void {
    this.selectedBuyer.set(b);
  }

  closeDrawer(): void {
    this.selectedBuyer.set(null);
  }

  openSuspendModal(org: AdminBuyer): void {
    this.suspendTarget.set(org);
    this.suspendReason = '';
  }

  closeModal(): void {
    this.suspendTarget.set(null);
  }

  confirmSuspend(orgId: string): void {
    if (!this.suspendReason.trim()) return;
    this.adminState.suspendBuyer(orgId, this.suspendReason);
    this.closeModal();
    if (this.selectedBuyer()?.orgId === orgId) {
      this.selectedBuyer.update(b => b ? { ...b, status: 'SUSPENDED', suspensionReason: this.suspendReason } : null);
    }
  }

  reactivateBuyer(orgId: string): void {
    this.adminState.reactivateBuyer(orgId);
    if (this.selectedBuyer()?.orgId === orgId) {
      this.selectedBuyer.update(b => b ? { ...b, status: 'ACTIVE', suspensionReason: undefined } : null);
    }
  }
}
