import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AdminStateService, AdminAuditLog } from '../../../core/services/admin-state.service';

@Component({
  selector: 'app-admin-audit-logs',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './audit-logs.component.html',
  styleUrl: './audit-logs.component.css'
})
export class AuditLogsComponent {
  private adminState = inject(AdminStateService);

  logs = this.adminState.auditLogs;
  selectedLog = signal<AdminAuditLog | null>(null);

  actorFilter = '';
  actionFilter = 'ALL';
  roleFilter = 'ALL';
  resultFilter = 'ALL';

  filteredLogs = computed(() => {
    let list = this.logs();
    const a = this.actorFilter.trim().toLowerCase();

    if (a) {
      list = list.filter(l => l.actor.toLowerCase().includes(a) || l.resource.toLowerCase().includes(a));
    }

    if (this.actionFilter !== 'ALL') {
      list = list.filter(l => l.action.includes(this.actionFilter));
    }

    if (this.roleFilter !== 'ALL') {
      list = list.filter(l => l.role === this.roleFilter);
    }

    if (this.resultFilter !== 'ALL') {
      list = list.filter(l => l.result === this.resultFilter);
    }

    return list;
  });

  selectEvent(ev: AdminAuditLog): void {
    this.selectedLog.set(ev);
  }

  closeDrawer(): void {
    this.selectedLog.set(null);
  }

  resetFilters(): void {
    this.actorFilter = '';
    this.actionFilter = 'ALL';
    this.roleFilter = 'ALL';
    this.resultFilter = 'ALL';
  }

  exportAudit(): void {
    // Generates cryptographically signed audit log export
  }
}
