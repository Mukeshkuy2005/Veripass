import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AdminStateService, AdminDocument } from '../../../core/services/admin-state.service';

@Component({
  selector: 'app-document-management',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './document-management.component.html',
  styleUrl: './document-management.component.css'
})
export class DocumentManagementComponent {
  private adminState = inject(AdminStateService);

  documents = this.adminState.documents;
  stats = this.adminState.documentStats;

  selectedDoc = signal<AdminDocument | null>(null);
  quarantineTarget = signal<AdminDocument | null>(null);
  quarantineReason = '';

  selectDoc(d: AdminDocument): void {
    this.selectedDoc.set(d);
  }

  closeDrawer(): void {
    this.selectedDoc.set(null);
  }

  openQuarantineModal(doc: AdminDocument): void {
    this.quarantineTarget.set(doc);
    this.quarantineReason = '';
  }

  closeQuarantineModal(): void {
    this.quarantineTarget.set(null);
  }

  confirmQuarantine(docId: string): void {
    if (!this.quarantineReason.trim()) return;
    this.adminState.quarantineDocument(docId, this.quarantineReason);
    this.closeQuarantineModal();
    if (this.selectedDoc()?.id === docId) {
      this.selectedDoc.update(d => d ? { ...d, status: 'QUARANTINE', quarantineReason: this.quarantineReason, risk: 'HIGH' } : null);
    }
  }
}
