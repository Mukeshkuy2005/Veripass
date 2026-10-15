import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AdminStateService, CredentialTypeConfig } from '../../../core/services/admin-state.service';

@Component({
  selector: 'app-credential-management',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './credential-management.component.html',
  styleUrl: './credential-management.component.css'
})
export class CredentialManagementComponent {
  private adminState = inject(AdminStateService);

  credentials = this.adminState.credentials;
  activeEditConfig = signal<CredentialTypeConfig | null>(null);

  openConfig(c: CredentialTypeConfig): void {
    // Clone config for editing
    this.activeEditConfig.set({
      ...c,
      sources: { ...c.sources }
    });
  }

  closeConfig(): void {
    this.activeEditConfig.set(null);
  }

  saveConfig(updated: CredentialTypeConfig): void {
    this.adminState.updateCredentialConfig(updated.code, updated);
    this.closeConfig();
  }
}
