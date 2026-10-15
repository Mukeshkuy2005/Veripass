import { Component, Input, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import * as QRCode from 'qrcode';
import { jsPDF } from 'jspdf';

@Component({
  selector: 'app-passport-quick-access',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './passport-quick-access.component.html',
  styleUrl: './passport-quick-access.component.css'
})
export class PassportQuickAccessComponent implements OnInit {
  @Input() passportId: string = 'VP-IN-PENDING';
  @Input() isVerified: boolean = false;
  @Input() trustScore: number = 0;
  @Input() companyName: string = '';
  @Input() verifiedPills: string[] = [];

  copied = signal(false);
  pdfDownloaded = signal(false);
  showQrModal = signal(false);
  qrDataUrl = signal<string>('');
  qrLoading = signal(true);

  ngOnInit(): void {
    this.generateQrCode();
  }

  generateQrCode(): void {
    const passportUrl = `https://veripass.in/passport/${this.passportId}`;
    QRCode.toDataURL(passportUrl, {
      width: 320,
      margin: 1,
      color: {
        dark: '#0F172A',
        light: '#FFFFFF'
      },
      errorCorrectionLevel: 'H'
    })
    .then((url: string) => {
      this.qrDataUrl.set(url);
      this.qrLoading.set(false);
    })
    .catch((err: any) => {
      console.error('Error generating dynamic QR code:', err);
      this.qrLoading.set(false);
    });
  }

  openInstantScanModal(): void {
    this.showQrModal.set(true);
  }

  closeInstantScanModal(): void {
    this.showQrModal.set(false);
  }

  downloadQrImage(): void {
    if (!this.qrDataUrl()) return;
    const a = document.createElement('a');
    a.href = this.qrDataUrl();
    a.download = `VERIPASS-QR-${this.passportId}.png`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
  }

  copyLink(): void {
    const url = `https://veripass.in/passport/${this.passportId}`;
    if (navigator.clipboard) {
      navigator.clipboard.writeText(url).then(() => {
        this.copied.set(true);
        setTimeout(() => this.copied.set(false), 2000);
      });
    }
  }

  isDownloadingPdf = signal(false);

  downloadPdf(): void {
    if (this.isDownloadingPdf()) return;
    this.isDownloadingPdf.set(true);

    try {
      const doc = new jsPDF({ orientation: 'portrait', unit: 'mm', format: 'a4' });
      const pageWidth = 210;

      // Outer Decorative Border
      doc.setDrawColor(29, 78, 216);
      doc.setLineWidth(1.2);
      doc.rect(10, 10, 190, 277);

      doc.setFillColor(17, 24, 39);
      doc.rect(12, 12, 186, 24, 'F');

      doc.setTextColor(255, 255, 255);
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(15);
      doc.text('VERIPASS COMMERCIAL PASSPORT', pageWidth / 2, 22, { align: 'center' });

      doc.setFontSize(8.5);
      doc.setFont('helvetica', 'normal');
      doc.setTextColor(147, 197, 253);
      doc.text('SOVEREIGN IDENTITY & TRUST SUMMARY', pageWidth / 2, 30, { align: 'center' });

      // Body Info
      doc.setTextColor(15, 23, 42);
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(11);
      doc.text(`Entity: ${this.companyName || 'Registered Enterprise'}`, 20, 50);

      doc.setFontSize(9.5);
      doc.text(`Passport ID: ${this.passportId}`, 20, 60);
      doc.text(`Trust Score: ${this.trustScore} / 100`, 20, 68);
      doc.text(`Status: ${this.isVerified ? 'VERIFIED (ACTIVE)' : 'IN REVIEW'}`, 20, 76);
      doc.text(`Issued On: ${new Date().toLocaleDateString('en-GB')}`, 20, 84);

      doc.setFont('helvetica', 'normal');
      doc.setFontSize(8.5);
      doc.setTextColor(100, 116, 139);
      doc.text(`Digital Verification URL: https://veripass.in/passport/${this.passportId}`, 20, 96);

      // Generate binary PDF blob (application/pdf)
      const blob: Blob = doc.output('blob');
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `VERIPASS-${this.passportId}-PASSPORT.pdf`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      setTimeout(() => URL.revokeObjectURL(url), 1000);

      this.isDownloadingPdf.set(false);
      this.pdfDownloaded.set(true);
      setTimeout(() => this.pdfDownloaded.set(false), 2500);
    } catch (err) {
      console.error('Failed to generate quick passport PDF:', err);
      this.isDownloadingPdf.set(false);
    }
  }
}
