import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';

@Component({
  selector: 'app-public-passport',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './public-passport.component.html',
  styleUrl: './public-passport.component.css'
})
export class PublicPassportComponent implements OnInit {
  private route = inject(ActivatedRoute);

  passportId = signal('VP-IN-2026-92644');
  shareToast = signal(false);

  businessData = signal({
    passportId: 'VP-IN-2026-92644',
    legalName: 'ABC INDUSTRIAL DYNAMICS',
    businessType: 'Private Limited Company',
    industry: 'Precision Engineering & Tooling',
    establishedYear: '2020',
    location: 'Mumbai, Maharashtra, India',
    website: 'abc-industrial.in',
    score: 88
  });

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const id = params.get('passportId') || params.get('publicIdentifier') || params.get('publicId');
      if (id) {
        this.passportId.set(id.toUpperCase());
        if (id.toUpperCase().includes('46190') || id.toUpperCase().includes('AXIONIS') || id.toUpperCase() === 'VP-XZY8921') {
          this.businessData.set({
            passportId: 'VP-IN-2026-46190',
            legalName: 'Axionis manufacturing pvt ltd',
            businessType: 'Private Limited Company',
            industry: 'IT & Electronic Systems',
            establishedYear: '2021',
            location: 'Bengaluru, Karnataka, India',
            website: 'axionis-mfg.in',
            score: 82
          });
        }
      }
    });
  }

  sharePassport(): void {
    if (navigator.clipboard) {
      navigator.clipboard.writeText(window.location.href);
    }
    this.shareToast.set(true);
    setTimeout(() => this.shareToast.set(false), 3000);
  }
}
