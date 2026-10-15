import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { EscalatedCasesComponent } from './escalated-cases.component';
import {
  VerifierEscalatedService,
  EscalatedCasesResponse,
  EscalatedCaseDetail
} from '../../../core/services/verifier-escalated.service';

describe('EscalatedCasesComponent', () => {
  let component: EscalatedCasesComponent;
  let fixture: ComponentFixture<EscalatedCasesComponent>;
  let mockEscalatedService: jasmine.SpyObj<VerifierEscalatedService>;

  const mockResponse: EscalatedCasesResponse = {
    items: [
      {
        caseId: 'VP-18291',
        caseGuid: '18291000-0000-0000-0000-000000000001',
        businessId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
        businessName: 'ABC Manufacturing Pvt Ltd',
        gstin: '09ABCDE1234F1Z5',
        verificationType: 'GST',
        escalationReason: 'Conflicting GST identity information',
        priority: 'HIGH',
        riskLevel: 'HIGH',
        riskScore: 72,
        escalatedAtUtc: '2026-10-15T04:37:00Z',
        escalatedByName: 'Rahul Sharma',
        assignedVerifierId: 'uuuuuuuu-uuuu-uuuu-uuuu-uuuuuuuuuuuu',
        assignedVerifierName: 'Rahul Sharma',
        status: 'ESCALATED',
        concurrencyToken: 123456789
      },
      {
        caseId: 'VP-18287',
        caseGuid: '22222222-2222-2222-2222-222222222222',
        businessId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
        businessName: 'XYZ Industries',
        gstin: '27AAACX1234F1Z8',
        verificationType: 'MSME',
        escalationReason: 'Evidence unclear',
        priority: 'MEDIUM',
        riskLevel: 'MEDIUM',
        riskScore: 38,
        escalatedAtUtc: '2026-10-15T03:15:00Z',
        escalatedByName: 'Rahul Sharma',
        assignedVerifierId: undefined,
        assignedVerifierName: undefined,
        status: 'ESCALATED',
        concurrencyToken: 987654321
      }
    ],
    page: 1,
    pageSize: 25,
    totalCount: 2,
    totalPages: 1,
    summary: {
      totalCount: 2,
      assignedCount: 1,
      unassignedCount: 1
    },
    serverTimeUtc: '2026-10-15T05:00:00Z'
  };

  const mockDetail: EscalatedCaseDetail = {
    caseId: 'VP-18291',
    caseGuid: '18291000-0000-0000-0000-000000000001',
    businessId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    businessName: 'ABC Manufacturing Pvt Ltd',
    gstin: '09ABCDE1234F1Z5',
    verificationType: 'GST',
    escalationReason: 'Conflicting GST identity information',
    priority: 'HIGH',
    riskLevel: 'HIGH',
    riskScore: 72,
    escalatedAtUtc: '2026-10-15T04:37:00Z',
    escalatedByName: 'Rahul Sharma',
    assignedVerifierId: 'uuuuuuuu-uuuu-uuuu-uuuu-uuuuuuuuuuuu',
    assignedVerifierName: 'Rahul Sharma',
    status: 'ESCALATED',
    concurrencyToken: 123456789,
    evidences: [
      {
        evidenceId: 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee',
        documentId: 'dddddddd-dddd-dddd-dddd-dddddddddddd',
        documentType: 'GST_CERTIFICATE',
        fileName: 'gst_certificate.pdf',
        status: 'VERIFIED',
        uploadedAtUtc: '2026-10-14T10:00:00Z'
      }
    ],
    previousReview: {
      verifierName: 'Rahul Sharma',
      decision: 'ESCALATED',
      attemptCount: 2,
      lastReviewedAtUtc: '2026-10-15T04:37:00Z'
    },
    canSeniorReview: true,
    canClaim: false
  };

  beforeEach(async () => {
    mockEscalatedService = jasmine.createSpyObj('VerifierEscalatedService', [
      'getEscalatedCases',
      'getEscalationDetail',
      'claimCase',
      'approveCase',
      'rejectCase',
      'returnCase'
    ]);

    mockEscalatedService.getEscalatedCases.and.returnValue(of(mockResponse));
    mockEscalatedService.getEscalationDetail.and.returnValue(of(mockDetail));
    mockEscalatedService.claimCase.and.returnValue(of({ success: true }));
    mockEscalatedService.approveCase.and.returnValue(of({ success: true }));
    mockEscalatedService.rejectCase.and.returnValue(of({ success: true }));
    mockEscalatedService.returnCase.and.returnValue(of({ success: true }));

    await TestBed.configureTestingModule({
      imports: [EscalatedCasesComponent],
      providers: [
        provideRouter([]),
        { provide: VerifierEscalatedService, useValue: mockEscalatedService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(EscalatedCasesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => {
    fixture.destroy();
  });

  it('should create component and load escalated cases on init', () => {
    expect(component).toBeTruthy();
    expect(mockEscalatedService.getEscalatedCases).toHaveBeenCalledWith(jasmine.objectContaining({
      view: 'all',
      page: 1,
      pageSize: 25
    }));
    expect(component.items().length).toBe(2);
    expect(component.summary().totalCount).toBe(2);
    expect(component.summary().assignedCount).toBe(1);
    expect(component.summary().unassignedCount).toBe(1);
  });

  it('should switch tabs to "mine" and reload cases', () => {
    component.setTab('mine');
    expect(component.selectedTab()).toBe('mine');
    expect(component.page()).toBe(1);
    expect(mockEscalatedService.getEscalatedCases).toHaveBeenCalledWith(jasmine.objectContaining({
      view: 'mine'
    }));
  });

  it('should switch tabs to "unassigned" and reload cases', () => {
    component.setTab('unassigned');
    expect(component.selectedTab()).toBe('unassigned');
    expect(mockEscalatedService.getEscalatedCases).toHaveBeenCalledWith(jasmine.objectContaining({
      view: 'unassigned'
    }));
  });

  it('should apply filters and reset pagination', () => {
    component.searchQuery.set('ABC Manufacturing');
    component.priorityFilter.set('HIGH');
    component.reasonFilter.set('Conflicting');
    component.page.set(3);

    component.applyFilters();

    expect(component.page()).toBe(1);
    expect(mockEscalatedService.getEscalatedCases).toHaveBeenCalledWith(jasmine.objectContaining({
      search: 'ABC Manufacturing',
      priority: 'HIGH',
      reason: 'Conflicting'
    }));
  });

  it('should reset filters to default and reload', () => {
    component.searchQuery.set('Something');
    component.priorityFilter.set('HIGH');
    component.resetFilters();

    expect(component.searchQuery()).toBe('');
    expect(component.priorityFilter()).toBe('ALL');
    expect(component.page()).toBe(1);
  });

  it('should open detail drawer and load escalation detail', () => {
    const item = mockResponse.items[0];
    component.openDrawer(item);

    expect(component.drawerOpen()).toBeTrue();
    expect(component.selectedCaseId()).toBe('VP-18291');
    expect(mockEscalatedService.getEscalationDetail).toHaveBeenCalledWith('VP-18291');
    expect(component.caseDetail()).toEqual(mockDetail);
  });

  it('should close drawer on Escape key or closeDrawer()', () => {
    component.openDrawer(mockResponse.items[0]);
    expect(component.drawerOpen()).toBeTrue();

    component.onEscapeKey();
    expect(component.drawerOpen()).toBeFalse();
    expect(component.selectedCaseId()).toBeNull();
  });

  it('should claim case successfully and reload queue', () => {
    const item = mockResponse.items[1];
    component.claimCase(item);

    expect(mockEscalatedService.claimCase).toHaveBeenCalledWith('VP-18287', 987654321);
    expect(component.actionSuccessMessage()).toContain('claimed successfully');
  });

  it('should approve case and refresh queue', fakeAsync(() => {
    component.caseDetail.set(mockDetail);
    component.approveCase();

    expect(mockEscalatedService.approveCase).toHaveBeenCalledWith('VP-18291', jasmine.any(String), 123456789);
    expect(component.actionSuccessMessage()).toContain('Case approved successfully');

    tick(1300);
    expect(component.drawerOpen()).toBeFalse();
  }));

  it('should reject case only when reason is provided', fakeAsync(() => {
    component.caseDetail.set(mockDetail);
    component.openConfirmModal('REJECT');

    // Submit with empty reason
    component.modalReason.set('   ');
    component.submitModalDecision();

    expect(component.modalReasonError()).toBe('A valid reason is required.');
    expect(mockEscalatedService.rejectCase).not.toHaveBeenCalled();

    // Submit with valid reason
    component.modalReason.set('Mismatch in statutory GST filing and physical address.');
    component.submitModalDecision();

    expect(mockEscalatedService.rejectCase).toHaveBeenCalledWith(
      'VP-18291',
      'Mismatch in statutory GST filing and physical address.',
      123456789
    );
    expect(component.actionSuccessMessage()).toContain('rejected successfully');

    tick(1300);
    expect(component.drawerOpen()).toBeFalse();
  }));

  it('should return case to verifier queue when reason is provided', fakeAsync(() => {
    component.caseDetail.set(mockDetail);
    component.openConfirmModal('RETURN');

    // Submit with valid return reason
    component.modalReason.set('Please request alternate electricity bill or lease deed.');
    component.submitModalDecision();

    expect(mockEscalatedService.returnCase).toHaveBeenCalledWith(
      'VP-18291',
      'Please request alternate electricity bill or lease deed.',
      123456789
    );
    expect(component.actionSuccessMessage()).toContain('returned to verifier queue successfully');

    tick(1300);
    expect(component.drawerOpen()).toBeFalse();
  }));

  it('should handle service error gracefully and display error banner', () => {
    mockEscalatedService.getEscalatedCases.and.returnValue(throwError(() => new Error('Server connection error')));
    component.loadEscalatedCases();

    expect(component.loading()).toBeFalse();
    expect(component.error()).toContain('Server connection error');
  });
});
