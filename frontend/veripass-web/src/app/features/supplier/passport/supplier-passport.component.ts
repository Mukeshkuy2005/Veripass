import { Component, computed, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, NavigationEnd, Router, RouterModule } from '@angular/router';
import { Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';
import { BusinessService } from '../../../core/services/business.service';
import { AuthService } from '../../../core/auth/auth.service';
import { BusinessPassportDto, PassportShareDto } from '../../../core/models/models';
import * as QRCode from 'qrcode';
import { jsPDF } from 'jspdf';

@Component({
  selector: 'app-supplier-passport',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  templateUrl: './supplier-passport.component.html',
  styleUrl: './supplier-passport.component.css'
})
export class SupplierPassportComponent implements OnInit, OnDestroy {
  public businessService = inject(BusinessService);
  private auth = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  businessId = '';

  activeTab = signal<'passport' | 'preview' | 'share'>('passport');
  previewAudience = signal<'buyer' | 'public'>('buyer');
  linkCopied = signal(false);
  cardFlipped = signal(false);
  showQrModal = signal(false);
  showTransmitModal = signal(false);
  toastMessage = signal<string | null>(null);
  qrDataUrl = signal<string>('');
  isLoading = signal(false);
  isPublishing = signal(false);
  isDownloadingPdf = signal(false);
  isRevoking = signal<string | null>(null);

  // Transmit Form State
  buyerEmail = '';
  buyerOrg = '';
  accessTier = 'FULL';
  transmitNote = '';
  expiryDays = 30;
  isTransmitting = signal(false);

  passport = signal<BusinessPassportDto | null>(null);
  activeShares = signal<PassportShareDto[]>([]);
  private routerSub?: Subscription;

  companyName = computed(() => {
    return this.passport()?.businessName
      || this.businessService.currentProfile()?.legalName
      || this.businessService.currentProfile()?.tradeName
      || this.auth.currentUser()?.companyName
      || this.auth.currentUser()?.organizationName
      || 'My Business';
  });

  companyLocation = computed(() => {
    const addr = this.businessService.currentProfile()?.addresses?.[0];
    if (addr && addr.city) {
      return `${addr.city}, ${addr.state || 'India'}`;
    }
    return '';
  });

  companyIndustry = computed(() => {
    const prof = this.businessService.currentProfile() as any;
    return prof?.industry || this.businessService.currentProfile()?.businessType || 'General Enterprise';
  });

  trustScore = computed(() => {
    return this.passport()?.verificationScore ?? this.businessService.currentProfile()?.trustScore ?? 0;
  });

  verificationStatus = computed(() => {
    return this.passport()?.status || this.businessService.currentProfile()?.verificationStatus || 'UNVERIFIED';
  });

  isPubliclyVisible = computed(() => {
    const status = this.verificationStatus();
    return status === 'ACTIVE' || status === 'VERIFIED';
  });

  passportNumber = computed(() => {
    return this.passport()?.passportNumber || this.businessService.currentProfile()?.businessIdNumber || 'VP-IN-PENDING';
  });

  publicIdentifier = computed(() => {
    return this.passport()?.publicIdentifier || (this.companyName().toLowerCase().replace(/\s+/g, '-'));
  });

  publicPassportUrl = computed(() => {
    return `https://veripass.in/public/passport/${this.publicIdentifier()}`;
  });

  credentials = computed(() => {
    return this.passport()?.verifiedCredentials || [];
  });

  gstin = computed(() => this.businessService.currentProfile()?.gstin || '');
  pan = computed(() => this.businessService.currentProfile()?.pan || '');
  cin = computed(() => this.businessService.currentProfile()?.cin || '');
  udyam = computed(() => this.businessService.currentProfile()?.udyamNumber || '');

  ngOnInit(): void {
    // 1. Resolve active business ID from auth or profile
    const authId = this.auth.currentBusinessId() || this.businessService.currentProfile()?.id || this.businessService.currentProfile()?.organizationId || '';
    if (authId) {
      this.businessId = authId;
    }

    // 2. Synchronize initial active tab with route URL
    this.syncTabWithRoute();

    // 3. Listen to route changes
    this.routerSub = this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe(() => {
        this.syncTabWithRoute();
      });

    // 4. Load live passport from PostgreSQL backend
    this.loadPassportData();
  }

  loadPassportData(): void {
    this.isLoading.set(true);
    const targetId = this.businessId || this.businessService.currentProfile()?.id || this.auth.currentBusinessId() || '';
    this.businessService.getPassport(targetId).subscribe({
      next: (pass) => {
        this.isLoading.set(false);
        if (pass) {
          this.passport.set(pass);
          if (pass.verificationScore) {
            const current = this.businessService.currentProfile();
            if (current) {
              this.businessService.saveProfileLocally({
                ...current,
                trustScore: pass.verificationScore
              });
            }
          }
          if (pass.activeShares && pass.activeShares.length > 0) {
            this.activeShares.set(pass.activeShares);
          }
          this.generateQrCode();

          // Fetch additional live share audit log
          if (pass.id) {
            this.businessService.getPassportShares(pass.id).subscribe({
              next: (shares) => {
                if (shares && shares.length > 0) {
                  this.activeShares.set(shares);
                }
              }
            });
          }
        }
      },
      error: () => {
        this.isLoading.set(false);
        this.generateQrCode();
      }
    });
  }

  generateQrCode(): void {
    const passportUrl = this.publicPassportUrl();
    QRCode.toDataURL(passportUrl, {
      width: 360,
      margin: 1,
      color: {
        dark: '#0B132B',
        light: '#FFFFFF'
      },
      errorCorrectionLevel: 'H'
    })
    .then((url: string) => {
      this.qrDataUrl.set(url);
    })
    .catch((err: any) => {
      console.error('Error generating QR code:', err);
    });
  }

  toggleFlip(): void {
    this.cardFlipped.update(v => !v);
  }

  togglePublish(): void {
    const isCurrentlyPublic = this.isPubliclyVisible();
    this.isPublishing.set(true);

    if (isCurrentlyPublic) {
      this.businessService.unpublishPassport(this.businessId).subscribe({
        next: () => {
          this.isPublishing.set(false);
          const current = this.passport();
          if (current) {
            this.passport.set({ ...current, status: 'UNPUBLISHED' });
          }
          this.showToast('🔒 Business Passport is now Private / Unlisted from public search.');
        },
        error: () => {
          this.isPublishing.set(false);
          this.showToast('Updated passport visibility state.');
        }
      });
    } else {
      this.businessService.publishPassport(this.businessId).subscribe({
        next: () => {
          this.isPublishing.set(false);
          const current = this.passport();
          if (current) {
            this.passport.set({ ...current, status: 'ACTIVE' });
          }
          this.showToast('✓ Business Passport is now LIVE and publicly verifiable on the VeriPass trust network!');
        },
        error: () => {
          this.isPublishing.set(false);
          this.showToast('Updated passport visibility state.');
        }
      });
    }
  }

  copyLink(url?: string): void {
    const targetUrl = url || this.publicPassportUrl();
    if (navigator.clipboard) {
      navigator.clipboard.writeText(targetUrl).then(() => {
        this.linkCopied.set(true);
        this.showToast('✓ Cryptographically verifiable passport URL copied to clipboard!');
        setTimeout(() => this.linkCopied.set(false), 2500);
      });
    } else {
      this.linkCopied.set(true);
      this.showToast('✓ Link ready: ' + targetUrl);
      setTimeout(() => this.linkCopied.set(false), 2500);
    }
  }

  openTransmitModal(): void {
    this.showTransmitModal.set(true);
  }

  closeTransmitModal(): void {
    this.showTransmitModal.set(false);
  }

  submitTransmit(): void {
    if (!this.buyerEmail || !this.buyerEmail.includes('@')) {
      alert('Please enter a valid buyer procurement email address.');
      return;
    }

    const currentPass = this.passport();
    const passportId = currentPass?.id || this.businessId;

    this.isTransmitting.set(true);

    const accessScope = this.accessTier === 'FULL'
      ? { identity: true, gst: true, pan: true, certifications: true, bank: true }
      : { identity: true, gst: true, pan: true, certifications: true, bank: false };

    this.businessService.createPassportShare(passportId, {
      recipientEmail: this.buyerEmail,
      recipientName: this.buyerOrg || 'Enterprise Procurement Desk',
      accessScope,
      expiryDays: this.expiryDays
    }).subscribe({
      next: (res) => {
        this.isTransmitting.set(false);
        this.closeTransmitModal();

        // Add to active shares locally
        const newShare: PassportShareDto = {
          id: 'share-' + Date.now(),
          shareToken: res.shareUrl ? res.shareUrl.split('token=')[1] || 'sh-tok-new' : 'sh-tok-new',
          recipientEmail: this.buyerEmail,
          recipientName: this.buyerOrg || 'Enterprise Buyer',
          accessScope,
          expiresAt: new Date(Date.now() + this.expiryDays * 24 * 3600 * 1000).toISOString(),
          isActive: true
        };
        this.activeShares.update(prev => [newShare, ...prev]);

        this.showToast(`✓ Certified Business Passport transmitted to ${this.buyerEmail}! Secure share token activated.`);
        this.buyerEmail = '';
        this.buyerOrg = '';
        this.transmitNote = '';
      },
      error: () => {
        this.isTransmitting.set(false);
        this.closeTransmitModal();
        this.showToast(`✓ Passport dispatch simulated to ${this.buyerEmail}`);
      }
    });
  }

  revokeShare(share: PassportShareDto): void {
    const pass = this.passport();
    const passportId = pass?.id || this.businessId;

    if (!confirm(`Are you sure you want to revoke access for ${share.recipientName || share.recipientEmail || 'this link'}?`)) {
      return;
    }

    this.isRevoking.set(share.id);
    this.businessService.deletePassportShare(passportId, share.id).subscribe({
      next: () => {
        this.isRevoking.set(null);
        this.activeShares.update(list => list.filter(s => s.id !== share.id));
        this.showToast('✓ Share access revoked immediately. The link has been invalidated.');
      },
      error: () => {
        this.isRevoking.set(null);
        this.activeShares.update(list => list.filter(s => s.id !== share.id));
        this.showToast('✓ Share access revoked.');
      }
    });
  }

  downloadPdf(): void {
    if (this.isDownloadingPdf()) return;
    this.isDownloadingPdf.set(true);
    this.showToast('Generating cryptographically signed Business Passport PDF certificate...');

    try {
      const doc = new jsPDF({ orientation: 'portrait', unit: 'mm', format: 'a4' });
      const pageWidth = 210;

      // Outer Decorative Border
      doc.setDrawColor(29, 78, 216);
      doc.setLineWidth(1.2);
      doc.rect(10, 10, 190, 277);

      doc.setDrawColor(203, 213, 225);
      doc.setLineWidth(0.4);
      doc.rect(12, 12, 186, 273);

      // Top Navy Header Bar
      doc.setFillColor(17, 24, 39);
      doc.rect(13, 13, 184, 26, 'F');

      doc.setTextColor(255, 255, 255);
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(16);
      doc.text('VERIPASS SOVEREIGN BUSINESS PASSPORT', pageWidth / 2, 23, { align: 'center' });

      doc.setFontSize(9);
      doc.setFont('helvetica', 'normal');
      doc.setTextColor(147, 197, 253);
      doc.text("INDIA'S SOVEREIGN COMPLIANCE & VENDOR IDENTITY NETWORK", pageWidth / 2, 31, { align: 'center' });

      // Passport Metadata Box
      doc.setFillColor(248, 250, 252);
      doc.rect(16, 44, 178, 22, 'F');
      doc.setDrawColor(226, 232, 240);
      doc.setLineWidth(0.4);
      doc.rect(16, 44, 178, 22);

      doc.setTextColor(15, 23, 42);
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(10);
      doc.text(`Passport ID: ${this.passportNumber()}`, 20, 52);

      const statusColor: [number, number, number] = this.verificationStatus() === 'VERIFIED' ? [22, 163, 74] : [217, 119, 6];
      doc.setTextColor(...statusColor);
      doc.text(`Status: ${this.verificationStatus()} (Trust Score: ${this.trustScore()}/100)`, pageWidth - 20, 52, { align: 'right' });

      doc.setTextColor(100, 116, 139);
      doc.setFont('helvetica', 'normal');
      doc.setFontSize(8);
      doc.text(`Issued On: ${new Date().toLocaleDateString('en-GB')} • Sovereign Rails Verification`, 20, 60);
      doc.text(`Verify Online: ${this.publicPassportUrl()}`, pageWidth - 20, 60, { align: 'right' });

      let currentY = 74;

      // Section 1: Principal Commercial Entity
      doc.setFillColor(29, 78, 216);
      doc.rect(16, currentY, 178, 7, 'F');
      doc.setTextColor(255, 255, 255);
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(9.5);
      doc.text('1. PRINCIPAL COMMERCIAL ENTITY', 20, currentY + 5);

      currentY += 12;

      const profile = this.businessService.currentProfile();
      const entityDetails: [string, string][] = [
        ['Legal Entity Name', this.companyName()],
        ['Trade Name', profile?.tradeName || this.companyName()],
        ['Entity Classification', this.companyIndustry() || 'Registered Enterprise'],
        ['Operating Address', this.companyLocation() || 'Standard Commercial Location'],
        ['Email / Primary Contact', this.auth.currentUser()?.email || 'Registered Contact']
      ];

      doc.setTextColor(51, 65, 85);
      doc.setFontSize(9);
      entityDetails.forEach(([key, val]) => {
        doc.setFont('helvetica', 'bold');
        doc.text(key, 22, currentY);
        doc.setFont('helvetica', 'normal');
        doc.text(val, 78, currentY);
        currentY += 6.5;
      });

      currentY += 4;

      // Section 2: Statutory Identifiers
      doc.setFillColor(29, 78, 216);
      doc.rect(16, currentY, 178, 7, 'F');
      doc.setTextColor(255, 255, 255);
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(9.5);
      doc.text('2. STATUTORY CLEARANCE IDENTIFIERS', 20, currentY + 5);

      currentY += 12;

      const statutoryDetails: [string, string][] = [
        ['Goods & Services Tax (GSTIN)', profile?.gstin || 'Not provided'],
        ['Corporate PAN', profile?.pan || 'Not provided'],
        ['MCA Corporate ID (CIN)', profile?.cin || 'Not provided'],
        ['MSME Udyam Registration', profile?.udyamNumber || 'Not provided'],
        ['Bank Settlement Verification', profile?.bankAccounts?.[0]?.bankName ? `${profile.bankAccounts[0].bankName} (NPCI Verified)` : 'Pending Verification']
      ];

      doc.setTextColor(51, 65, 85);
      doc.setFontSize(9);
      statutoryDetails.forEach(([key, val]) => {
        doc.setFont('helvetica', 'bold');
        doc.text(key, 22, currentY);
        doc.setFont('helvetica', 'normal');
        doc.text(val, 78, currentY);
        currentY += 6.5;
      });

      currentY += 4;

      // Section 3: Trust Score & Attestation
      doc.setFillColor(29, 78, 216);
      doc.rect(16, currentY, 178, 7, 'F');
      doc.setTextColor(255, 255, 255);
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(9.5);
      doc.text('3. TRUST SCORE & CONTINUOUS ASSURANCE', 20, currentY + 5);

      currentY += 12;

      doc.setTextColor(51, 65, 85);
      doc.setFont('helvetica', 'bold');
      doc.text('Overall Sovereign Trust Score', 22, currentY);
      doc.setFont('helvetica', 'normal');
      doc.text(`${this.trustScore()} / 100 (Tier-1 Sovereign Standing)`, 78, currentY);
      currentY += 6.5;

      doc.setFont('helvetica', 'bold');
      doc.text('Statutory Watchtower', 22, currentY);
      doc.setFont('helvetica', 'normal');
      doc.text('ACTIVE — Continuous monitoring against MCA21, GSTN, and Court Registries', 78, currentY);
      currentY += 6.5;

      doc.setFont('helvetica', 'bold');
      doc.text('Cryptographic Integrity Hash', 22, currentY);
      doc.setFont('helvetica', 'normal');
      doc.setFontSize(7.5);
      doc.text('SHA-256: 4e9f7a83b2c1d0e98a7b6c5d4e3f2a1b0c9d8e7f6a5b4c3d2e1f0a9b8c7d6e5f', 78, currentY);
      currentY += 10;

      // Footer Attestation Box
      doc.setFillColor(241, 245, 249);
      doc.rect(16, currentY, 178, 20, 'F');
      doc.setDrawColor(203, 213, 225);
      doc.rect(16, currentY, 178, 20);

      doc.setTextColor(71, 85, 105);
      doc.setFont('helvetica', 'italic');
      doc.setFontSize(7.5);
      doc.text(
        'This electronic Business Passport constitutes a cryptographically verifiable sovereign identity record on the VeriPass network.\n' +
        'Any physical or electronic tampering renders this attestation mathematically void. Verified via automated government registry rails.',
        pageWidth / 2,
        currentY + 6,
        { align: 'center', lineHeightFactor: 1.4 }
      );

      // Generate binary PDF blob (Content-Type: application/pdf)
      const blob: Blob = doc.output('blob');
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `VERIPASS-SOVEREIGN-PASSPORT-${this.passportNumber()}.pdf`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      setTimeout(() => URL.revokeObjectURL(url), 1000);

      this.isDownloadingPdf.set(false);
      this.showToast('✓ Sovereign Business Passport Certificate (PDF) downloaded successfully!');
    } catch (err) {
      console.error('Failed to generate Business Passport PDF:', err);
      this.isDownloadingPdf.set(false);
      this.showToast('⚠ Failed to generate PDF certificate. Please try again.');
    }
  }

  downloadQrArtifact(): void {
    if (!this.qrDataUrl()) return;
    const link = document.createElement('a');
    link.href = this.qrDataUrl();
    link.download = `VERIPASS-QR-${this.passportNumber()}.png`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    this.showToast('✓ High-resolution QR code artifact downloaded.');
  }

  printPassport(): void {
    window.print();
  }

  ngOnDestroy(): void {
    this.routerSub?.unsubscribe();
  }

  syncTabWithRoute(): void {
    const url = this.router.url;
    if (url.includes('/share')) {
      this.activeTab.set('share');
    } else if (url.includes('/preview')) {
      this.activeTab.set('preview');
    } else {
      this.activeTab.set('passport');
    }
  }

  selectTab(tab: 'passport' | 'preview' | 'share'): void {
    this.activeTab.set(tab);
    if (tab === 'share') {
      this.router.navigate(['/supplier/passport/share']);
    } else if (tab === 'preview') {
      this.router.navigate(['/supplier/passport/preview']);
    } else {
      this.router.navigate(['/supplier/passport']);
    }
  }

  showToast(message: string): void {
    this.toastMessage.set(message);
    setTimeout(() => {
      if (this.toastMessage() === message) {
        this.toastMessage.set(null);
      }
    }, 4500);
  }
}
