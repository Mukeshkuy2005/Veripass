import { Component, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';

export interface BuyerSettingsTab {
  id: 'org' | 'team' | 'roles' | 'security' | 'notifications' | 'procurement' | 'integrations' | 'api' | 'account';
  label: string;
  icon: string;
  description: string;
}

export interface TeamMember {
  id: string;
  name: string;
  email: string;
  role: string;
  status: 'Active' | 'Invited' | 'Suspended';
  avatarInitials: string;
}

export interface ApiKeyItem {
  id: string;
  name: string;
  prefix: string;
  fullKey?: string;
  created: string;
  lastUsed: string;
  status: 'Active' | 'Revoked';
}

@Component({
  selector: 'app-buyer-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './buyer-settings.component.html',
  styleUrl: './buyer-settings.component.css'
})
export class BuyerSettingsComponent {
  private authService = inject(AuthService);

  activeTab = signal<'org' | 'team' | 'roles' | 'security' | 'notifications' | 'procurement' | 'integrations' | 'api' | 'account'>('org');

  tabs: BuyerSettingsTab[] = [
    { id: 'org', label: 'Organization Profile', icon: '🏢', description: 'Legal entity details & sovereign ID' },
    { id: 'team', label: 'Team Members', icon: '👥', description: 'Procurement users & invitations' },
    { id: 'roles', label: 'Roles & Permissions', icon: '🛡️', description: 'Granular enterprise RBAC matrix' },
    { id: 'security', label: 'Security & Auth', icon: '🔒', description: 'MFA, active sessions & SSO' },
    { id: 'notifications', label: 'Notification Rules', icon: '🔔', description: 'Supplier & risk alert triggers' },
    { id: 'procurement', label: 'Procurement Policies', icon: '⚙️', description: 'Baseline thresholds & compliance' },
    { id: 'integrations', label: 'ERP & Systems', icon: '🔌', description: 'SAP, Oracle & Teams connections' },
    { id: 'api', label: 'Developer & API', icon: '⚡', description: 'API credentials & webhooks' },
    { id: 'account', label: 'Account Governance', icon: '🏛️', description: 'Sovereignty, exports & audit' }
  ];

  currentUser = this.authService.currentUser;
  companyName = computed(() => this.currentUser()?.companyName || this.currentUser()?.organizationName || 'Enterprise Buyer');
  userInitials = computed(() => {
    const user = this.currentUser();
    const name = user?.fullName || `${user?.firstName || ''} ${user?.lastName || ''}`.trim() || 'Procurement Admin';
    return name.split(' ').filter(Boolean).map(p => p[0]).slice(0, 2).join('').toUpperCase() || 'PA';
  });

  org = {
    companyName: this.currentUser()?.companyName || this.currentUser()?.organizationName || 'Registered Enterprise Buyer',
    industry: 'Enterprise Procurement & Sourcing',
    size: 'Commercial Enterprise',
    category: 'Commercial Sourcing & Vendor Operations',
    id: this.currentUser()?.currentOrganizationId ? `ORG-${this.currentUser()?.currentOrganizationId?.substring(0, 8).toUpperCase()}` : 'ORG-VERIFIED',
    gstin: 'Pending Verification',
    pan: 'Pending Verification',
    registeredAddress: 'Registered Commercial Headquarters, India',
    sovereignRegion: 'Republic of India (MeitY Sovereign Cloud Network)'
  };

  teamMembers = signal<TeamMember[]>([
    {
      id: this.currentUser()?.id || 'tm-1',
      name: this.currentUser()?.fullName || `${this.currentUser()?.firstName || 'Procurement'} ${this.currentUser()?.lastName || 'Admin'}`.trim(),
      email: this.currentUser()?.email || 'admin@organization.in',
      role: 'Procurement Admin (Primary)',
      status: 'Active',
      avatarInitials: this.currentUser()?.firstName ? (this.currentUser()?.firstName?.[0] || 'P') + (this.currentUser()?.lastName?.[0] || 'A') : 'PA'
    }
  ]);

  rbacPermissions = [
    { name: 'Search & Discover Suppliers', admin: true, procurement: '✓ Full', compliance: '✓ Full', viewer: '✓ Full' },
    { name: 'View Sovereign Passport Credentials', admin: true, procurement: '✓ Full', compliance: '✓ Full', viewer: '✓ Read Only' },
    { name: 'Shortlist & Evaluate Vendors', admin: true, procurement: '✓ Full', compliance: '✓ Full', viewer: '✕ Restriced' },
    { name: 'Vendor Comparison Matrix', admin: true, procurement: '✓ Full', compliance: '✓ Full', viewer: '✓ View Only' },
    { name: 'Trigger Verification Requests', admin: true, procurement: '✓ Full', compliance: '✓ Full', viewer: '✕ No Access' },
    { name: 'Approve & Onboard Supplier (ERP Code)', admin: true, procurement: '✓ Approval Only', compliance: '✕ Sign-off Only', viewer: '✕ No Access' },
    { name: 'Manage Team & Assign Roles', admin: true, procurement: '✕ Restricted', compliance: '✕ Restricted', viewer: '✕ No Access' },
    { name: 'Access Audit Logs & Telemetry', admin: true, procurement: 'Limited', compliance: '✓ Full', viewer: '✕ No Access' },
    { name: 'Organization Settings & Governance', admin: true, procurement: '✕ Restricted', compliance: '✕ Restricted', viewer: '✕ No Access' },
    { name: 'API Key Generation & Webhooks', admin: true, procurement: '✕ Restricted', compliance: '✕ Restricted', viewer: '✕ No Access' }
  ];

  // API Keys state
  apiKeys = signal<ApiKeyItem[]>([]);

  // Notifications
  notificationConfig = {
    verifCompleted: true,
    verifFailed: true,
    credentialExpiring: true,
    onboardingAccepted: true,
    allDocsSubmitted: true,
    onboardingApproved: true,
    riskDetected: true,
    complianceMismatch: true,
    weeklyDigest: false
  };

  // Procurement rules
  procurementRules = {
    minScore: '80+ (Tier-1 Quality & Statutory Verified)',
    complianceBaseline: 'Active GSTR-3B & Zero Default in 24 Months',
    onboardingSla: '15 business days',
    pennyDropValidation: true,
    sanctionScreening: true
  };

  // Modals & transient notices
  showInviteModal = signal(false);
  newMemberName = signal('');
  newMemberEmail = signal('');
  newMemberRole = signal('Strategic Sourcing');

  showCreateKeyModal = signal(false);
  newKeyLabel = signal('');
  generatedSecretKey = signal<string | null>(null);
  copiedSecretNotice = signal(false);

  showDeleteAccountModal = signal(false);
  deleteConfirmInput = signal('');
  isDeletingAccount = signal(false);

  saveNotice = signal<string | null>(null);
  exportingData = signal(false);
  exportComplete = signal(false);

  selectTab(tab: BuyerSettingsTab['id']): void {
    this.activeTab.set(tab);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  showNotification(msg: string): void {
    this.saveNotice.set(msg);
    setTimeout(() => {
      this.saveNotice.set(null);
    }, 4000);
  }

  saveOrg(): void {
    this.showNotification('Organization profile details updated and synced across procurement network.');
  }

  // Team management
  openInviteModal(): void {
    this.newMemberName.set('');
    this.newMemberEmail.set('');
    this.newMemberRole.set('Strategic Sourcing');
    this.showInviteModal.set(true);
  }

  closeInviteModal(): void {
    this.showInviteModal.set(false);
  }

  sendInvite(): void {
    const email = this.newMemberEmail().trim();
    const name = this.newMemberName().trim() || email.split('@')[0];
    if (!email) return;

    const initials = name.split(' ').map(p => p[0]).slice(0, 2).join('').toUpperCase() || 'TM';
    this.teamMembers.update(members => [
      ...members,
      {
        id: 'tm-' + Date.now(),
        name,
        email,
        role: this.newMemberRole(),
        status: 'Invited',
        avatarInitials: initials
      }
    ]);
    this.closeInviteModal();
    this.showNotification(`Enterprise invitation dispatched to ${email}`);
  }

  removeMember(id: string): void {
    this.teamMembers.update(members => members.filter(m => m.id !== id));
    this.showNotification('Team member access revoked.');
  }

  // Security
  revokeSessions(): void {
    this.showNotification('All other concurrent sessions revoked. Active device retained.');
  }

  saveNotifications(): void {
    this.showNotification('Procurement telemetry & notification triggers saved.');
  }

  saveProcurementPolicies(): void {
    this.showNotification('Procurement evaluation baseline and pre-qualification criteria updated.');
  }

  // API Key management
  openCreateKeyModal(): void {
    this.newKeyLabel.set('');
    this.generatedSecretKey.set(null);
    this.copiedSecretNotice.set(false);
    this.showCreateKeyModal.set(true);
  }

  closeCreateKeyModal(): void {
    this.showCreateKeyModal.set(false);
    this.generatedSecretKey.set(null);
  }

  generateApiKey(): void {
    const label = this.newKeyLabel().trim() || 'Custom Microservice Client';
    const rawSecret = 'vp_live_' + Array.from({ length: 32 }, () => Math.floor(Math.random() * 36).toString(36)).join('');
    this.generatedSecretKey.set(rawSecret);

    const newKeyItem: ApiKeyItem = {
      id: 'key-' + Date.now(),
      name: label,
      prefix: rawSecret.substring(0, 16) + '...',
      fullKey: rawSecret,
      created: new Date().toISOString().split('T')[0],
      lastUsed: 'Never',
      status: 'Active'
    };

    this.apiKeys.update(keys => [newKeyItem, ...keys]);
  }

  copySecretKey(): void {
    const key = this.generatedSecretKey();
    if (!key) return;
    navigator.clipboard?.writeText(key).then(() => {
      this.copiedSecretNotice.set(true);
      setTimeout(() => this.copiedSecretNotice.set(false), 2500);
    });
  }

  revokeApiKey(id: string): void {
    this.apiKeys.update(keys =>
      keys.map(k => k.id === id ? { ...k, status: 'Revoked' } : k)
    );
    this.showNotification('API key immediately invalidated.');
  }

  // Account governance
  triggerDataExport(): void {
    this.exportingData.set(true);
    setTimeout(() => {
      this.exportingData.set(false);
      this.exportComplete.set(true);

      // Create browser download payload for Sovereign Audit JSON
      const exportData = {
        organization: this.org,
        exportedAt: new Date().toISOString(),
        teamMembers: this.teamMembers(),
        apiKeysConfigured: this.apiKeys().length,
        dataJurisdiction: 'Republic of India (MeitY)',
        complianceStandard: 'ISO 27001 / SOC-2 Type II'
      };

      const blob = new Blob([JSON.stringify(exportData, null, 2)], { type: 'application/json' });
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `VERIPASS-BUYER-SOVEREIGN-EXPORT-${this.org.id}.json`;
      a.click();
      window.URL.revokeObjectURL(url);

      this.showNotification('Enterprise Sovereign audit archive generated and downloaded.');
      setTimeout(() => this.exportComplete.set(false), 5000);
    }, 1200);
  }

  openDeleteModal(): void {
    this.deleteConfirmInput.set('');
    this.showDeleteAccountModal.set(true);
  }

  closeDeleteModal(): void {
    this.showDeleteAccountModal.set(false);
  }

  confirmAccountTermination(): void {
    if (this.deleteConfirmInput() !== 'DELETE') return;
    this.isDeletingAccount.set(true);
    setTimeout(() => {
      this.isDeletingAccount.set(false);
      this.closeDeleteModal();
      this.showNotification('Enterprise termination request logged. Compliance team notified for legal archive.');
    }, 1000);
  }
}
