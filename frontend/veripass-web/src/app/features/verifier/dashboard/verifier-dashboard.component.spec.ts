import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { VerifierDashboardComponent } from './verifier-dashboard.component';
import { VerifierDashboardService } from './services/verifier-dashboard.service';
import { VerifierDashboardResponse } from './models/verifier-dashboard.model';

describe('VerifierDashboardComponent', () => {
  let component: VerifierDashboardComponent;
  let fixture: ComponentFixture<VerifierDashboardComponent>;
  let mockDashboardService: jasmine.SpyObj<VerifierDashboardService>;

  const mockApiResponse: VerifierDashboardResponse = {
    summary: {
      pending: 126,
      pendingTodayDelta: 12,
      inReview: 38,
      assignedToCurrentVerifier: 14,
      approved: 512,
      approvedTodayDelta: 27,
      rejected: 17,
      escalated: 6
    },
    priorityCases: [
      {
        id: '11111111-1111-1111-1111-111111111111',
        priority: 'HIGH',
        businessName: 'ABC INDUSTRIAL DYNAMICS',
        verificationType: 'GST',
        submittedAt: new Date(Date.now() - 18 * 60000).toISOString(),
        riskLevel: 'HIGH',
        status: 'PENDING',
        caseNumber: 'VP-18291'
      },
      {
        id: '22222222-2222-2222-2222-222222222222',
        priority: 'MEDIUM',
        businessName: 'Axionis manufacturing pvt ltd',
        verificationType: 'Address',
        submittedAt: new Date(Date.now() - 42 * 60000).toISOString(),
        riskLevel: 'MEDIUM',
        status: 'PENDING',
        caseNumber: 'VP-18292'
      }
    ],
    performance: {
      casesReviewedToday: 42,
      averageReviewTimeSeconds: 452,
      approvalRate: 86.0,
      escalationRate: 4.2
    },
    riskDistribution: {
      low: 71.0,
      medium: 23.0,
      high: 6.0
    },
    recentDecisions: [
      {
        id: '33333333-3333-3333-3333-333333333333',
        verificationType: 'GST',
        decision: 'APPROVED',
        businessName: 'ABC INDUSTRIAL DYNAMICS',
        timestamp: new Date().toISOString(),
        verifierName: 'Rahul Sharma'
      },
      {
        id: '44444444-4444-4444-4444-444444444444',
        verificationType: 'ISO',
        decision: 'REJECTED',
        businessName: 'Axionis manufacturing pvt ltd',
        timestamp: new Date().toISOString(),
        verifierName: 'Amit Kumar'
      }
    ]
  };

  beforeEach(async () => {
    mockDashboardService = jasmine.createSpyObj('VerifierDashboardService', [
      'loadDashboard',
      'refreshDashboard',
      'clearRefreshError'
    ], {
      dashboardData: jasmine.createSpy('dashboardData').and.returnValue(mockApiResponse),
      summary: jasmine.createSpy('summary').and.returnValue(mockApiResponse.summary),
      priorityCases: jasmine.createSpy('priorityCases').and.returnValue(mockApiResponse.priorityCases),
      performance: jasmine.createSpy('performance').and.returnValue(mockApiResponse.performance),
      riskDistribution: jasmine.createSpy('riskDistribution').and.returnValue(mockApiResponse.riskDistribution),
      recentDecisions: jasmine.createSpy('recentDecisions').and.returnValue(mockApiResponse.recentDecisions),
      isLoading: jasmine.createSpy('isLoading').and.returnValue(false),
      isRefreshing: jasmine.createSpy('isRefreshing').and.returnValue(false),
      error: jasmine.createSpy('error').and.returnValue(null),
      refreshError: jasmine.createSpy('refreshError').and.returnValue(null),
      lastRefreshedAt: jasmine.createSpy('lastRefreshedAt').and.returnValue(new Date()),
      hasData: jasmine.createSpy('hasData').and.returnValue(true)
    });

    mockDashboardService.loadDashboard.and.returnValue(of(mockApiResponse));
    mockDashboardService.refreshDashboard.and.returnValue(of(mockApiResponse));

    await TestBed.configureTestingModule({
      imports: [VerifierDashboardComponent],
      providers: [
        provideRouter([]),
        { provide: VerifierDashboardService, useValue: mockDashboardService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(VerifierDashboardComponent);
    component = fixture.componentInstance;
  });

  it('should create and initialize dashboard on startup', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
    expect(mockDashboardService.loadDashboard).toHaveBeenCalled();
  });

  it('should render exact operational KPI values from database response', () => {
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    const pendingCard = el.querySelector('#kpi-card-pending .kpi-num');
    expect(pendingCard?.textContent?.trim()).toBe('126');

    const inReviewCard = el.querySelector('#kpi-card-in-review .kpi-num');
    expect(inReviewCard?.textContent?.trim()).toBe('38');

    const approvedCard = el.querySelector('#kpi-card-approved .kpi-num');
    expect(approvedCard?.textContent?.trim()).toBe('512');

    const rejectedCard = el.querySelector('#kpi-card-rejected .kpi-num');
    expect(rejectedCard?.textContent?.trim()).toBe('17');

    const escalatedCard = el.querySelector('#kpi-card-escalated .kpi-num');
    expect(escalatedCard?.textContent?.trim()).toBe('6');
  });

  it('should render priority cases with appropriate badges and links', () => {
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    const caseItems = el.querySelectorAll('.case-item');
    expect(caseItems.length).toBe(2);

    const firstCaseTitle = el.querySelector('#case-item-11111111-1111-1111-1111-111111111111 .case-title');
    expect(firstCaseTitle?.textContent).toContain('ABC INDUSTRIAL DYNAMICS');
    expect(firstCaseTitle?.textContent).toContain('GST Verification');

    const reviewBtn = el.querySelector('#btn-review-11111111-1111-1111-1111-111111111111');
    expect(reviewBtn?.getAttribute('href')).toContain('/verifier/cases/11111111-1111-1111-1111-111111111111');
  });

  it('should render operational performance metrics accurately', () => {
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    const reviewedToday = el.querySelector('#perf-cases-reviewed .pm-val');
    expect(reviewedToday?.textContent?.trim()).toBe('42');

    const avgTime = el.querySelector('#perf-avg-time .pm-val');
    // 452 seconds = 7m 32s
    expect(avgTime?.textContent?.trim()).toBe('7m 32s');

    const approvalRate = el.querySelector('#perf-approval-rate .pm-val');
    expect(approvalRate?.textContent?.trim()).toBe('86%');

    const escalationRate = el.querySelector('#perf-escalation-rate .pm-val');
    expect(escalationRate?.textContent?.trim()).toBe('4.2%');
  });

  it('should render risk distribution percentages accurately with progressbars', () => {
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    const lowPct = el.querySelector('#risk-row-low .dist-pct');
    expect(lowPct?.textContent?.trim()).toBe('71%');

    const medPct = el.querySelector('#risk-row-medium .dist-pct');
    expect(medPct?.textContent?.trim()).toBe('23%');

    const highPct = el.querySelector('#risk-row-high .dist-pct');
    expect(highPct?.textContent?.trim()).toBe('6%');
  });

  it('should render recent decisions with semantic indicators and case navigation', () => {
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    const decisionCards = el.querySelectorAll('.decision-card');
    expect(decisionCards.length).toBe(2);

    const firstCard = el.querySelector('#decision-card-33333333-3333-3333-3333-333333333333');
    expect(firstCard?.textContent).toContain('Approved');
    expect(firstCard?.textContent).toContain('ABC INDUSTRIAL DYNAMICS');
    expect(firstCard?.getAttribute('href')).toContain('/verifier/cases/33333333-3333-3333-3333-333333333333');
  });

  it('should trigger service refresh when refresh button is clicked', () => {
    fixture.detectChanges();
    const refreshBtn: HTMLButtonElement | null = fixture.nativeElement.querySelector('#btn-dashboard-refresh');
    expect(refreshBtn).toBeTruthy();

    refreshBtn?.click();
    expect(mockDashboardService.refreshDashboard).toHaveBeenCalled();
  });
});
