import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SupplierDocumentsComponent } from './supplier-documents.component';
import { BusinessService } from '../../../core/services/business.service';
import { AuthService } from '../../../core/auth/auth.service';
import { RouterTestingModule } from '@angular/router/testing';
import { of, throwError } from 'rxjs';
import { signal } from '@angular/core';

describe('SupplierDocumentsComponent - Add Credential Flow', () => {
  let component: SupplierDocumentsComponent;
  let fixture: ComponentFixture<SupplierDocumentsComponent>;
  let businessServiceMock: any;
  let authServiceMock: any;

  beforeEach(async () => {
    businessServiceMock = {
      currentProfile: signal({
        id: 'biz-123',
        legalName: 'Apex Precision Tools Ltd',
        verificationStatus: 'VERIFIED',
        trustScore: 92,
        addresses: [{ line1: 'Plot 44', city: 'Faridabad', state: 'Haryana', pincode: '121004' }]
      }),
      getDocuments: jasmine.createSpy('getDocuments').and.returnValue(of([
        {
          id: 'doc-1',
          documentType: 'GST_REGISTRATION',
          storageKey: '06AAAAA0000A1Z5',
          originalFileName: 'gst_certificate.pdf',
          status: 'PROCESSED',
          uploadedAt: '2026-10-14T10:00:00Z'
        }
      ])),
      uploadDocument: jasmine.createSpy('uploadDocument').and.returnValue(of({
        documentId: 'doc-new',
        status: 'PROCESSED'
      }))
    };

    authServiceMock = {
      currentBusinessId: signal('biz-123'),
      currentUser: signal({
        companyName: 'Apex Precision Tools Ltd'
      })
    };

    await TestBed.configureTestingModule({
      imports: [SupplierDocumentsComponent, RouterTestingModule],
      providers: [
        { provide: BusinessService, useValue: businessServiceMock },
        { provide: AuthService, useValue: authServiceMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SupplierDocumentsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  describe('Modal Open & Reset Behavior', () => {
    it('opens Add Credential modal and resets form inputs', () => {
      component.newCredName.set('Stale Name');
      component.openAddCredentialModal();

      expect(component.showAddCredentialModal()).toBeTrue();
      expect(component.newCredName()).toBe('');
      expect(component.newCredFile()).toBeNull();
      expect(component.addCredentialError()).toBeNull();
    });

    it('closes Add Credential modal cleanly when not submitting', () => {
      component.openAddCredentialModal();
      component.closeAddCredentialModal();

      expect(component.showAddCredentialModal()).toBeFalse();
    });

    it('prevents closing the modal while upload is in flight', () => {
      component.openAddCredentialModal();
      component.isSubmittingCredential.set(true);

      component.closeAddCredentialModal();

      expect(component.showAddCredentialModal()).toBeTrue();
    });
  });

  describe('Upload Validation (MIME, Extension, Size)', () => {
    it('rejects unsupported file formats (.exe, .zip, .txt)', () => {
      const invalidFile = new File(['dummy content'], 'trojan.exe', { type: 'application/x-msdownload' });
      const event = { target: { files: [invalidFile] } };

      component.onCredentialFileSelected(event);

      expect(component.newCredFile()).toBeNull();
      expect(component.addCredentialError()).toContain('Invalid file format');
    });

    it('rejects oversized files exceeding 10MB', () => {
      const oversizedBytes = new Uint8Array(11 * 1024 * 1024); // 11 MB
      const largeFile = new File([oversizedBytes], 'large_doc.pdf', { type: 'application/pdf' });
      const event = { target: { files: [largeFile] } };

      component.onCredentialFileSelected(event);

      expect(component.newCredFile()).toBeNull();
      expect(component.addCredentialError()).toContain('exceeds the 10 MB limit');
    });

    it('accepts valid PDF within 10MB limit and autocompletes credential title', () => {
      const validFile = new File(['valid content'], 'iso_9001_cert.pdf', { type: 'application/pdf' });
      const event = { target: { files: [validFile] } };

      component.onCredentialFileSelected(event);

      expect(component.newCredFile()).toBe(validFile);
      expect(component.newCredFileName()).toBe('iso_9001_cert.pdf');
      expect(component.newCredName()).toContain('iso 9001 cert');
      expect(component.addCredentialError()).toBeNull();
    });
  });

  describe('Duplicate Prevention & Double-Click Guard', () => {
    it('detects and blocks duplicate credentials with existing identifier or name', () => {
      const validFile = new File(['content'], 'gst_copy.pdf', { type: 'application/pdf' });
      component.newCredFile.set(validFile);
      component.newCredName.set('GST REGISTRATION');
      component.newCredNumber.set('06AAAAA0000A1Z5'); // Already in credentials()

      component.submitNewCredential();

      expect(component.addCredentialError()).toContain('already registered');
      expect(businessServiceMock.uploadDocument).not.toHaveBeenCalled();
    });

    it('guards against double-click submission while submission is in progress', () => {
      const validFile = new File(['content'], 'new_cert.pdf', { type: 'application/pdf' });
      component.newCredFile.set(validFile);
      component.newCredName.set('Brand New Environmental NOC');
      component.isSubmittingCredential.set(true); // submission in progress

      component.submitNewCredential();

      expect(businessServiceMock.uploadDocument).not.toHaveBeenCalled();
    });
  });

  describe('Submission & List Refresh', () => {
    it('successfully uploads credential, closes modal, and refreshes documents', () => {
      const validFile = new File(['content'], 'udyam_registration.pdf', { type: 'application/pdf' });
      component.newCredFile.set(validFile);
      component.newCredName.set('MSME Udyam Certificate');
      component.newCredType.set('MSME_UDYAM');
      component.showAddCredentialModal.set(true);

      component.submitNewCredential();

      expect(businessServiceMock.uploadDocument).toHaveBeenCalledWith(
        'biz-123',
        'MSME_UDYAM',
        validFile
      );
      expect(component.showAddCredentialModal()).toBeFalse();
      expect(businessServiceMock.getDocuments).toHaveBeenCalled();
    });

    it('displays error message when API fails (network or server error)', () => {
      businessServiceMock.uploadDocument.and.returnValue(throwError(() => ({ status: 500, error: { message: 'Upload service down' } })));
      const validFile = new File(['content'], 'udyam_registration.pdf', { type: 'application/pdf' });
      component.newCredFile.set(validFile);
      component.newCredName.set('MSME Udyam Certificate');

      component.submitNewCredential();

      expect(component.isSubmittingCredential()).toBeFalse();
      expect(component.addCredentialError()).toContain('Upload service down');
    });

    it('displays session expired message when upload returns 401', () => {
      businessServiceMock.uploadDocument.and.returnValue(throwError(() => ({ status: 401 })));
      const validFile = new File(['content'], 'udyam_registration.pdf', { type: 'application/pdf' });
      component.newCredFile.set(validFile);
      component.newCredName.set('MSME Udyam Certificate');

      component.submitNewCredential();

      expect(component.isSubmittingCredential()).toBeFalse();
      expect(component.addCredentialError()).toContain('session has expired');
    });
  });
});
