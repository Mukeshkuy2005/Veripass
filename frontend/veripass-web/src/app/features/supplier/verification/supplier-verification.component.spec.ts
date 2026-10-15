import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SupplierVerificationComponent } from './supplier-verification.component';
import { VerificationService } from '../../../core/services/verification.service';
import { BusinessService } from '../../../core/services/business.service';
import { AuthService } from '../../../core/auth/auth.service';
import { RouterTestingModule } from '@angular/router/testing';
import { of, throwError } from 'rxjs';
import { signal } from '@angular/core';

describe('SupplierVerificationComponent', () => {
  let component: SupplierVerificationComponent;
  let fixture: ComponentFixture<SupplierVerificationComponent>;
  let businessServiceMock: any;
  let authServiceMock: any;
  let verificationServiceMock: any;

  beforeEach(async () => {
    businessServiceMock = {
      currentProfile: signal({
        id: 'biz-123',
        legalName: 'Apex Precision Tools Ltd',
        verificationStatus: 'VERIFIED',
        trustScore: 94,
        gstin: '06AAAAA0000A1Z5',
        pan: 'AAACA0000A',
        cin: 'U29299DL2020PTC368888',
        addresses: [{ line1: 'Plot 44', city: 'Faridabad', state: 'Haryana', pincode: '121004' }]
      }),
      uploadDocument: jasmine.createSpy('uploadDocument').and.returnValue(of({
        documentId: 'doc-addr-proof',
        status: 'PROCESSED'
      }))
    };

    authServiceMock = {
      currentBusinessId: signal('biz-123'),
      currentUser: signal({
        companyName: 'Apex Precision Tools Ltd'
      })
    };

    verificationServiceMock = {};

    await TestBed.configureTestingModule({
      imports: [SupplierVerificationComponent, RouterTestingModule],
      providers: [
        { provide: BusinessService, useValue: businessServiceMock },
        { provide: AuthService, useValue: authServiceMock },
        { provide: VerificationService, useValue: verificationServiceMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SupplierVerificationComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  describe('Statutory Identity Checklist Rendering', () => {
    it('renders all statutory checklist items', () => {
      const checkItems = fixture.nativeElement.querySelectorAll('.check-item');
      expect(checkItems.length).toBeGreaterThan(0);
      expect(checkItems.length).toBe(component.checks().length);
    });

    it('verifies PDF badge is removed everywhere in the Statutory Identity Checklist', () => {
      const checklistContainer = fixture.nativeElement.querySelector('.checklist-card');
      expect(checklistContainer).not.toBeNull();

      // Check text content inside every check item for any "PDF" badge/chip
      const checkItems = checklistContainer.querySelectorAll('.check-item');
      checkItems.forEach((item: HTMLElement) => {
        const badges = item.querySelectorAll('.auth-pill-tag, .meta-id, .meta-time, .status-sym, button');
        badges.forEach((b: Element) => {
          expect(b.textContent?.trim()).not.toBe('PDF');
          expect(b.textContent?.trim()).not.toBe('(PDF)');
        });
      });
    });
  });

  describe('Verification Upload Validation (Address Resolution)', () => {
    it('rejects unsupported file formats in address proof upload', () => {
      component.openAddressResolution();
      const invalidFile = new File(['binary'], 'archive.rar', { type: 'application/x-rar' });
      const event = { target: { files: [invalidFile] } };

      component.onFileSelected(event);

      expect(component.selectedProofFile()).toBeNull();
      expect(component.resolutionError()).toContain('Invalid file type');
    });

    it('rejects oversized files exceeding 10MB in address proof upload', () => {
      component.openAddressResolution();
      const oversizedBytes = new Uint8Array(12 * 1024 * 1024);
      const largeFile = new File([oversizedBytes], 'huge_bill.pdf', { type: 'application/pdf' });
      const event = { target: { files: [largeFile] } };

      component.onFileSelected(event);

      expect(component.selectedProofFile()).toBeNull();
      expect(component.resolutionError()).toContain('exceeds the 10 MB limit');
    });

    it('accepts valid PDF utility bill within 10MB', () => {
      component.openAddressResolution();
      const validFile = new File(['bill data'], 'electricity_oct2026.pdf', { type: 'application/pdf' });
      const event = { target: { files: [validFile] } };

      component.onFileSelected(event);

      expect(component.selectedProofFile()).toBe(validFile);
      expect(component.proofFileName()).toBe('electricity_oct2026.pdf');
      expect(component.resolutionError()).toBeNull();
    });
  });

  describe('Address Resolution Upload & Error Handling', () => {
    it('handles session expired (401) with clean user message', () => {
      businessServiceMock.uploadDocument.and.returnValue(throwError(() => ({ status: 401 })));
      component.openAddressResolution();
      component.selectedProofFile.set(new File(['content'], 'bill.pdf', { type: 'application/pdf' }));
      component.confirmAmendment.set(true);

      component.submitAddressResolution();

      expect(component.isResolving()).toBeFalse();
      expect(component.resolutionError()).toContain('session has expired');
    });

    it('handles network failure with clear retry message', () => {
      businessServiceMock.uploadDocument.and.returnValue(throwError(() => ({ status: 0, error: { message: 'Network offline' } })));
      component.openAddressResolution();
      component.selectedProofFile.set(new File(['content'], 'bill.pdf', { type: 'application/pdf' }));
      component.confirmAmendment.set(true);

      component.submitAddressResolution();

      expect(component.isResolving()).toBeFalse();
      expect(component.resolutionError()).toContain('Network offline');
    });

    it('prevents submission if amendment checkbox is not certified', () => {
      component.openAddressResolution();
      component.selectedProofFile.set(new File(['content'], 'bill.pdf', { type: 'application/pdf' }));
      component.confirmAmendment.set(false);

      component.submitAddressResolution();

      expect(component.resolutionError()).toContain('certify the statutory registered premises');
      expect(businessServiceMock.uploadDocument).not.toHaveBeenCalled();
    });
  });
});
