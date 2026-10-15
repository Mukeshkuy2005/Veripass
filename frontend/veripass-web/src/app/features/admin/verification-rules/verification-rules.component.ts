import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AdminStateService, VerificationRule } from '../../../core/services/admin-state.service';

@Component({
  selector: 'app-verification-rules',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './verification-rules.component.html',
  styleUrl: './verification-rules.component.css'
})
export class VerificationRulesComponent {
  private adminState = inject(AdminStateService);

  rules = this.adminState.rules;
  selectedRule = signal<VerificationRule | null>(null);
  showCreateModal = signal(false);

  newRule = {
    name: 'Corporate CIN Validation Protocol',
    type: 'Identity' as const,
    version: 'v1',
    status: 'ACTIVE' as const,
    appliesTo: 'Corporate MCA Filing',
    whenField: 'Credential Type',
    whenOperator: 'equals',
    whenValue: 'CIN',
    andField: 'Entity Type',
    andOperator: 'equals',
    andValue: 'Private Limited Company',
    thenAction: 'Query MCA Master Company Registry',
    failureAction: 'Create Verification Case',
    priority: 'HIGH' as const,
    effectiveFrom: '06 Sep 2026'
  };

  selectRule(r: VerificationRule): void {
    this.selectedRule.set(r);
  }

  closeDrawer(): void {
    this.selectedRule.set(null);
  }

  openCreateRuleModal(): void {
    this.showCreateModal.set(true);
  }

  closeCreateModal(): void {
    this.showCreateModal.set(false);
  }

  saveDraft(): void {
    this.adminState.createRule({
      ...this.newRule,
      status: 'DRAFT'
    });
    this.closeCreateModal();
  }

  publishDirectly(): void {
    this.adminState.createRule({
      ...this.newRule,
      status: 'ACTIVE'
    });
    this.closeCreateModal();
  }

  publishSelectedRule(ruleId: string): void {
    this.adminState.publishRule(ruleId, 'Published to production from admin workstation.');
    if (this.selectedRule()?.id === ruleId) {
      this.selectedRule.update(r => r ? { ...r, status: 'ACTIVE' } : null);
    }
  }
}
