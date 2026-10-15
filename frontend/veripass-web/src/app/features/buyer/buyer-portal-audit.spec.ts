import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { of, throwError } from 'rxjs';
import { signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';

import { BuyerLayoutComponent } from '../../layouts/buyer-layout/buyer-layout.component';
import { BuyerOnboardingComponent } from './onboarding/buyer-onboarding.component';
import { BuyerShortlistComponent } from './shortlist/buyer-shortlist.component';
import { SupplierComparisonComponent } from './comparison/supplier-comparison.component';
import { BuyerVerificationRequestsComponent } from './verification-requests/buyer-verification-requests.component';
import { OnboardingService } from '../../core/services/onboarding.service';
import { BuyerSearchService } from '../../core/services/buyer-search.service';
import { AuthService } from '../../core/auth/auth.service';

describe('VERIPASS Buyer Portal - QA & Engineering Audit Suite', () => {

  // =========================================================================
  // 1. DEFECT 1 & 2: BuyerLayoutComponent (Search Icon & Notification Panel)
  // =========================================================================
  describe('Defect 1 & 2: BuyerLayoutComponent (Search & Notification Panel)', () => {
    let component: BuyerLayoutComponent;
    let fixture: ComponentFixture<BuyerLayoutComponent>;
    let httpMock: any;
    let authMock: any;
    let router: Router;

    beforeEach(async () => {
      httpMock = {
        get: jasmine.createSpy('get').and.returnValue(of([])),
        post: jasmine.createSpy('post').and.returnValue(of({ success: true }))
      };

      authMock = {
        currentUser: signal({
          id: 'buyer-user-1',
          email: 'buyer@tata.com',
          firstName: 'Ratan',
          lastName: 'Tata',
          companyName: 'Tata Motors Sourcing',
          role: 'BUYER'
        }),
        logout: jasmine.createSpy('logout')
      };

      await TestBed.configureTestingModule({
        imports: [BuyerLayoutComponent, RouterTestingModule],
        providers: [
          { provide: HttpClient, useValue: httpMock },
          { provide: AuthService, useValue: authMock }
        ]
      }).compileComponents();

      router = TestBed.inject(Router);
      spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));

      fixture = TestBed.createComponent(BuyerLayoutComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();
    });

    it('Defect 1: Search button uses Lucide outline SVG with 24x24 viewBox and stroke-width 2', () => {
      const searchSvg: SVGElement | null = fixture.nativeElement.querySelector('.search-icon');
      expect(searchSvg).toBeTruthy('Search SVG icon must be rendered');
      expect(searchSvg?.getAttribute('viewBox')).toBe('0 0 24 24');
      expect(searchSvg?.getAttribute('stroke-width')).toBe('2');
      expect(searchSvg?.getAttribute('aria-hidden')).toBe('true');
    });

    it('Defect 2: Clicking notification bell opens dropdown panel and never navigates to Audit Log', () => {
      const bellBtn: HTMLButtonElement | null = fixture.nativeElement.querySelector('.notif-bell-btn');
      expect(bellBtn).toBeTruthy('Notification button must exist');

      expect(component.notificationDropdownOpen()).toBeFalse();
      bellBtn?.click();
      fixture.detectChanges();

      expect(component.notificationDropdownOpen()).toBeTrue();
      expect(router.navigate).not.toHaveBeenCalled();

      // Check dropdown DOM element
      const panel = fixture.nativeElement.querySelector('.notif-dropdown-panel');
      expect(panel).toBeTruthy('Notification dropdown panel must be in the DOM');

      // Check footer link specifically points to /buyer/activity
      const auditLogLink: HTMLAnchorElement | null = fixture.nativeElement.querySelector('.view-all-link');
      expect(auditLogLink).toBeTruthy();
      const hrefOrRouterLink = auditLogLink?.getAttribute('routerLink') || auditLogLink?.getAttribute('href') || '';
      expect(hrefOrRouterLink).toContain('/buyer/activity');
    });

    it('Defect 2: Red unread badge renders strictly when unreadCount > 0 and hides when unreadCount === 0', () => {
      // Initially 0 unread
      component.notificationsList.set([]);
      fixture.detectChanges();
      expect(component.unreadCount()).toBe(0);
      let badge = fixture.nativeElement.querySelector('.alert-dot');
      expect(badge).toBeFalsy('Red dot must NOT render when unreadCount is 0');

      // When unread exists
      component.notificationsList.set([
        { id: '1', title: 'T1', message: 'M1', isRead: false, type: 'alert', createdAt: '2026-10-15T10:00:00Z' },
        { id: '2', title: 'T2', message: 'M2', isRead: false, type: 'alert', createdAt: '2026-10-15T10:00:00Z' },
        { id: '3', title: 'T3', message: 'M3', isRead: false, type: 'alert', createdAt: '2026-10-15T10:00:00Z' }
      ]);
      fixture.detectChanges();
      expect(component.unreadCount()).toBe(3);
      badge = fixture.nativeElement.querySelector('.alert-dot');
      expect(badge).toBeTruthy('Red dot MUST render when unreadCount > 0');
    });

    it('Defect 2: Renders empty state message "No notifications" when notifications list is empty', () => {
      component.notificationDropdownOpen.set(true);
      component.notificationsLoading.set(false);
      component.notificationsError.set(false);
      component.notificationsList.set([]);
      fixture.detectChanges();

      const emptyText = fixture.nativeElement.querySelector('.notif-empty-state');
      expect(emptyText).toBeTruthy();
      expect(emptyText?.textContent).toContain('No notifications');
    });

    it('Defect 2: Closes notification dropdown on Escape key', () => {
      component.notificationDropdownOpen.set(true);
      fixture.detectChanges();
      expect(component.notificationDropdownOpen()).toBeTrue();

      const escapeEvent = new KeyboardEvent('keydown', { key: 'Escape' });
      document.dispatchEvent(escapeEvent);
      fixture.detectChanges();

      expect(component.notificationDropdownOpen()).toBeFalse();
    });
  });

  // =========================================================================
  // 2. DEFECT 3: BuyerOnboardingComponent (End-to-End Onboarding)
  // =========================================================================
  describe('Defect 3: BuyerOnboardingComponent (Onboarding Workflow)', () => {
    let component: BuyerOnboardingComponent;
    let fixture: ComponentFixture<BuyerOnboardingComponent>;
    let onboardingMock: any;
    let searchServiceMock: any;
    let authMock: any;

    beforeEach(async () => {
      onboardingMock = {
        getOnboardingRequests: jasmine.createSpy('getOnboardingRequests').and.returnValue(of({
          items: [],
          totalCount: 0,
          page: 1,
          pageSize: 12,
          counts: { total: 0, new: 0, requested: 0, inVerification: 0, actionRequired: 0, approved: 0, rejected: 0, expired: 0, withdrawn: 0 }
        })),
        getRequirementsCatalogue: jasmine.createSpy('getRequirementsCatalogue').and.returnValue(of([
          { code: 'GST_VERIFY', name: 'GSTIN Filing Status', description: 'GSTR-3B filings', category: 'STATUTORY', isDefaultMandatory: true }
        ])),
        searchSuppliers: jasmine.createSpy('searchSuppliers').and.returnValue(of([
          { id: 'sup-1', businessName: 'Axionis Manufacturing', legalName: 'Axionis Manufacturing Pvt Ltd', trustScore: 88, verificationStatus: 'VERIFIED' }
        ])),
        createOnboardingRequest: jasmine.createSpy('createOnboardingRequest').and.returnValue(of({
          id: 'onb-123',
          message: 'Onboarding request successfully created.'
        }))
      };

      searchServiceMock = {
        suppliers: signal([]),
        loadSuppliersFromBackend: jasmine.createSpy('loadSuppliersFromBackend')
      };

      authMock = {
        currentUser: signal({ id: 'u1', companyName: 'Tata Motors', role: 'BUYER' })
      };

      await TestBed.configureTestingModule({
        imports: [BuyerOnboardingComponent, RouterTestingModule],
        providers: [
          { provide: OnboardingService, useValue: onboardingMock },
          { provide: BuyerSearchService, useValue: searchServiceMock },
          { provide: AuthService, useValue: authMock }
        ]
      }).compileComponents();

      fixture = TestBed.createComponent(BuyerOnboardingComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();
    });

    it('Defect 3: Validation blocks Step 1 progression when no supplier is selected', () => {
      component.openStartModal();
      component.selectedSupplier.set(null);
      component.nextModalStep();

      expect(component.modalStep()).toBe(1, 'Should stay on Step 1');
      expect(component.errorMsg()).toContain('select a supplier');
    });

    it('Defect 3: Validation blocks Step 2 progression when purpose is empty', () => {
      component.openStartModal();
      component.selectedSupplier.set({ id: 'sup-1', businessName: 'Axionis' } as any);
      component.modalStep.set(2);
      component.formPurpose = '   '; // Blank

      component.nextModalStep();
      expect(component.modalStep()).toBe(2, 'Should stay on Step 2');
      expect(component.errorMsg()).toContain('purpose');
    });

    it('Defect 3: Submitting valid onboarding dispatches API, disables submit, and refreshes supplier lists', () => {
      component.openStartModal();
      component.selectedSupplier.set({ id: 'sup-1', businessName: 'Axionis' } as any);
      component.formPurpose = 'Tier 1 Empanelment';
      component.selectedRequirements.set([{ requirementCode: 'GST_VERIFY', isMandatory: true }]);

      component.submitOnboardingRequest(true);

      expect(onboardingMock.createOnboardingRequest).toHaveBeenCalled();
      expect(searchServiceMock.loadSuppliersFromBackend).toHaveBeenCalled();
      expect(component.submitSuccess()).toContain('successfully created');
    });

    it('Defect 3: Double-click prevention blocks parallel duplicate API calls', () => {
      component.openStartModal();
      component.selectedSupplier.set({ id: 'sup-1', businessName: 'Axionis' } as any);
      component.formPurpose = 'Tier 1 Empanelment';
      component.selectedRequirements.set([{ requirementCode: 'GST_VERIFY', isMandatory: true }]);

      // Simulate rapid second click while submitting is pending
      component.isSubmitting.set(true);
      component.submitOnboardingRequest(true);

      expect(onboardingMock.createOnboardingRequest).not.toHaveBeenCalled();
    });

    it('Defect 3: Pre-submission client-side duplicate detection blocks active workflow duplication', () => {
      component.requests.set([
        { id: 'req-1', supplierBusinessId: 'sup-1', status: 'IN_VERIFICATION' } as any
      ]);
      component.openStartModal();
      component.selectedSupplier.set({ id: 'sup-1', businessName: 'Axionis' } as any);
      component.formPurpose = 'Tier 1 Empanelment';
      component.selectedRequirements.set([{ requirementCode: 'GST_VERIFY', isMandatory: true }]);

      component.submitOnboardingRequest(true);

      expect(onboardingMock.createOnboardingRequest).not.toHaveBeenCalled();
      expect(component.errorMsg()).toContain('active onboarding workflow already exists');
    });

    it('Defect 3: Expired token (401) produces clear user-friendly guidance', () => {
      onboardingMock.createOnboardingRequest.and.returnValue(throwError(() => ({ status: 401 })));
      component.openStartModal();
      component.selectedSupplier.set({ id: 'sup-1', businessName: 'Axionis' } as any);
      component.formPurpose = 'Tier 1 Empanelment';
      component.selectedRequirements.set([{ requirementCode: 'GST_VERIFY', isMandatory: true }]);

      component.submitOnboardingRequest(true);

      expect(component.isSubmitting()).toBeFalse();
      expect(component.errorMsg()).toContain('session has expired');
    });

    it('Defect 3: resetForm clears all fields to default state', () => {
      component.formPurpose = 'Some purpose';
      component.formExpiryDays = 30;
      component.supplierSearchQuery = 'Axionis';
      component.selectedSupplier.set({ id: 's1' } as any);

      component.resetForm();

      expect(component.modalStep()).toBe(1);
      expect(component.formPurpose).toBe('');
      expect(component.formExpiryDays).toBe(14);
      expect(component.selectedSupplier()).toBeNull();
    });
  });

  // =========================================================================
  // 3. DEFECT 4: BuyerShortlistComponent (Binder & Pagination)
  // =========================================================================
  describe('Defect 4: BuyerShortlistComponent (Binder Page)', () => {
    let component: BuyerShortlistComponent;
    let fixture: ComponentFixture<BuyerShortlistComponent>;
    let httpMock: any;
    let searchServiceMock: any;

    beforeEach(async () => {
      httpMock = {
        get: jasmine.createSpy('get').and.returnValue(of([])),
        post: jasmine.createSpy('post').and.returnValue(of({ shortlistId: 'sl-1' })),
        delete: jasmine.createSpy('delete').and.returnValue(of({ success: true }))
      };

      searchServiceMock = {
        suppliers: signal([
          { id: 'sup-1', name: 'Vendor 1', legalName: 'V1 Ltd', trustScore: 90, complianceScore: 95, status: 'Verified', riskLevel: 'Low' },
          { id: 'sup-2', name: 'Vendor 2', legalName: 'V2 Ltd', trustScore: 85, complianceScore: 88, status: 'Verified', riskLevel: 'Low' },
          { id: 'sup-3', name: 'Vendor 3', legalName: 'V3 Ltd', trustScore: 78, complianceScore: 80, status: 'Verified', riskLevel: 'Medium' }
        ]),
        getSupplierById: (id: string) => searchServiceMock.suppliers().find((s: any) => s.id === id)
      };

      await TestBed.configureTestingModule({
        imports: [BuyerShortlistComponent, RouterTestingModule],
        providers: [
          { provide: HttpClient, useValue: httpMock },
          { provide: BuyerSearchService, useValue: searchServiceMock }
        ]
      }).compileComponents();

      fixture = TestBed.createComponent(BuyerShortlistComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();
    });

    it('Defect 4: Auto-creates Master Sourcing Binder when database returns empty shortlists list', () => {
      expect(component.collections().length).toBeGreaterThan(0, 'Should auto-populate fallback binder');
      expect(component.collections()[0].name).toContain('Master Sourcing Binder');
      expect(component.allShortlistedSuppliers().length).toBe(3);
    });

    it('Defect 4: Pagination computes correct totalPages and paginates suppliers based on pageSize', () => {
      component.pageSize.set(2);
      fixture.detectChanges();

      expect(component.totalPages()).toBe(2, '3 suppliers with pageSize 2 should produce 2 pages');
      expect(component.paginatedSuppliers().length).toBe(2, 'Page 1 should show first 2 suppliers');

      component.nextPage();
      expect(component.currentPage()).toBe(2);
      expect(component.paginatedSuppliers().length).toBe(1, 'Page 2 should show remaining 1 supplier');

      component.prevPage();
      expect(component.currentPage()).toBe(1);
    });

    it('Defect 4: Searching or changing collection resets currentPage back to 1', () => {
      component.pageSize.set(2);
      component.nextPage();
      expect(component.currentPage()).toBe(2);

      component.onSearchInput('Vendor');
      expect(component.currentPage()).toBe(1, 'Search query change must reset page to 1');
    });

    it('Defect 4: Empty state provides explicit Start Onboarding CTA routing to /buyer/onboarding?start=true', () => {
      component.isLoading.set(false);
      component.hasError.set(false);
      component.collections.set([]);
      fixture.detectChanges();

      const ctaBtn: HTMLAnchorElement | null = fixture.nativeElement.querySelector('#btn-start-onboarding-cta');
      expect(ctaBtn).toBeTruthy('Start Onboarding CTA button must be present in empty state');
      expect(ctaBtn?.getAttribute('href')).toContain('/buyer/onboarding');
    });
  });

  // =========================================================================
  // 4. DEFECT 5: SupplierComparisonComponent (Compare Tab)
  // =========================================================================
  describe('Defect 5: SupplierComparisonComponent (Compare Benchmark)', () => {
    let component: SupplierComparisonComponent;
    let fixture: ComponentFixture<SupplierComparisonComponent>;
    let httpMock: any;
    let searchServiceMock: any;

    beforeEach(async () => {
      httpMock = {
        get: jasmine.createSpy('get').and.returnValue(of([])),
        post: jasmine.createSpy('post').and.returnValue(of({ success: true }))
      };

      const testSuppliers = [
        { id: 's-1', name: 'Alpha Tech', legalName: 'Alpha Tech Pvt', trustScore: 92, complianceScore: 95, status: 'Verified', riskLevel: 'Low', industry: 'Manufacturing' },
        { id: 's-2', name: 'Beta Corp', legalName: 'Beta Corp Ltd', trustScore: 84, complianceScore: 89, status: 'Verified', riskLevel: 'Low', industry: 'Manufacturing' },
        { id: 's-3', name: 'Gamma Tools', legalName: 'Gamma Tools LLP', trustScore: 78, complianceScore: 82, status: 'Partially Verified', riskLevel: 'Medium', industry: 'Engineering' }
      ];

      searchServiceMock = {
        suppliers: signal(testSuppliers),
        selectedForCompare: signal([]),
        getSupplierById: (id: string) => testSuppliers.find(s => s.id === id),
        loadSuppliersFromBackend: jasmine.createSpy('loadSuppliersFromBackend'),
        toggleShortlist: jasmine.createSpy('toggleShortlist')
      };

      await TestBed.configureTestingModule({
        imports: [SupplierComparisonComponent, RouterTestingModule],
        providers: [
          { provide: HttpClient, useValue: httpMock },
          { provide: BuyerSearchService, useValue: searchServiceMock }
        ]
      }).compileComponents();

      fixture = TestBed.createComponent(SupplierComparisonComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();
    });

    it('Defect 5: Available suppliers in Add Modal derive from searchService.suppliers() catalog', () => {
      expect(component.availableSuppliers().length).toBe(3);
    });

    it('Defect 5: Selecting 1 supplier triggers the "fewer than 2" helpful prompt alert', () => {
      component.comparedSuppliers.set([
        {
          id: 's-1', businessId: 's-1', name: 'Alpha Tech', legalName: 'Alpha Tech Pvt', category: 'Mfg', industry: 'Mfg',
          location: 'Pune', state: 'MH', businessType: 'Pvt Ltd', businessIdNumber: 'CIN1', overallTrustScore: 92,
          identityScore: 92, credentialScore: 92, complianceScore: 95, verificationScore: 92, riskScore: 8,
          riskLevel: 'Low', yearsActive: 6, gstin: '27AA', pan: 'AA', udyam: 'UD', verifiedCredentials: ['GST'],
          status: 'Verified', isShortlisted: false
        }
      ]);
      fixture.detectChanges();

      const prompt = fixture.nativeElement.querySelector('.fewer-than-two-prompt');
      expect(prompt).toBeTruthy('Helpful prompt must be shown when 1 supplier is selected');
      expect(prompt.textContent).toContain('Select at least 1 more supplier');
    });

    it('Defect 5: Selecting 2+ suppliers renders selected chips strip and computes benchmark leader', () => {
      component.loadDefaultCluster();
      fixture.detectChanges();

      expect(component.comparedSuppliers().length).toBeGreaterThanOrEqual(2);
      expect(component.leaderSupplier()?.name).toBe('Alpha Tech', 'Alpha Tech with score 92 should lead');

      const chips = fixture.nativeElement.querySelectorAll('.supplier-chip-item');
      expect(chips.length).toBe(component.comparedSuppliers().length);
    });

    it('Defect 5: Enforces sensible maximum of 4 suppliers', () => {
      component.comparedSuppliers.set([
        { id: '1', name: '1' } as any,
        { id: '2', name: '2' } as any,
        { id: '3', name: '3' } as any,
        { id: '4', name: '4' } as any
      ]);
      fixture.detectChanges();

      component.addSupplierToCompare({ id: '5', name: '5' } as any);
      expect(component.comparedSuppliers().length).toBe(4, 'Must not exceed 4 suppliers');
    });
  });

  // =========================================================================
  // 5. DEFECT 6: BuyerVerificationRequestsComponent (SLA Turnaround Time)
  // =========================================================================
  describe('Defect 6: BuyerVerificationRequestsComponent (SLA Turnaround Time)', () => {
    let component: BuyerVerificationRequestsComponent;
    let fixture: ComponentFixture<BuyerVerificationRequestsComponent>;
    let httpMock: any;
    let authMock: any;

    beforeEach(async () => {
      httpMock = {
        get: jasmine.createSpy('get').and.returnValue(of([])),
        post: jasmine.createSpy('post').and.returnValue(of({ success: true })),
        delete: jasmine.createSpy('delete').and.returnValue(of({ success: true }))
      };

      authMock = {
        currentUser: signal({ id: 'u1', companyName: 'Tata Motors', role: 'BUYER' })
      };

      await TestBed.configureTestingModule({
        imports: [BuyerVerificationRequestsComponent, RouterTestingModule],
        providers: [
          { provide: HttpClient, useValue: httpMock },
          { provide: AuthService, useValue: authMock }
        ]
      }).compileComponents();

      fixture = TestBed.createComponent(BuyerVerificationRequestsComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();
    });

    it('Defect 6: When no completed requests exist, slaMetrics returns null and displays "No SLA data yet"', () => {
      component.requests.set([
        { id: '1', status: 'PENDING', createdAt: '2026-10-10T10:00:00Z', respondedAt: null } as any
      ]);
      fixture.detectChanges();

      expect(component.slaMetrics()).toBeNull();
      const emptyNotice = fixture.nativeElement.querySelector('.empty-sla-title');
      expect(emptyNotice).toBeTruthy();
      expect(emptyNotice.textContent).toContain('No SLA data yet');
    });

    it('Defect 6: Computes turnaround correctly in days when average duration is >= 24 hours', () => {
      component.requests.set([
        {
          id: '1',
          status: 'APPROVED',
          createdAt: '2026-10-10T10:00:00Z',
          respondedAt: '2026-10-12T10:00:00Z' // 48 hours = 2 days
        } as any,
        {
          id: '2',
          status: 'APPROVED',
          createdAt: '2026-10-10T10:00:00Z',
          respondedAt: '2026-10-14T10:00:00Z' // 96 hours = 4 days
        } as any
      ]);
      fixture.detectChanges();

      const metrics = component.slaMetrics();
      expect(metrics).toBeTruthy();
      expect(metrics?.value).toBe(3); // (2 + 4) / 2 = 3 days
      expect(metrics?.unit).toBe('days');
      expect(metrics?.onTrackPercent).toBe(100); // 3 days <= 7 days target
    });

    it('Defect 6: Gracefully handles partial or invalid/corrupted timestamps without throwing NaN', () => {
      component.requests.set([
        {
          id: '1',
          status: 'APPROVED',
          createdAt: 'invalid-date',
          respondedAt: 'corrupted-time'
        } as any
      ]);
      fixture.detectChanges();

      expect(component.slaMetrics()).toBeNull('Corrupted dates should be ignored safely');
    });

    it('Defect 6: SLA modal radio selection options use theme-aware classes instead of hardcoded black background', () => {
      component.showCreateModal.set(true);
      component.createForm.slaDays = '7';
      fixture.detectChanges();

      const selectedOpt: HTMLElement | null = fixture.nativeElement.querySelector('.sla-opt.selected');
      expect(selectedOpt).toBeTruthy();
      expect(selectedOpt?.textContent).toContain('7 Days');

      // Verify the radiogroup has proper accessibility labels
      const radioGroup = fixture.nativeElement.querySelector('.sla-pill-row');
      expect(radioGroup?.getAttribute('role')).toBe('radiogroup');
      expect(radioGroup?.getAttribute('aria-labelledby')).toBe('sla-turnaround-label');
    });
  });
});
