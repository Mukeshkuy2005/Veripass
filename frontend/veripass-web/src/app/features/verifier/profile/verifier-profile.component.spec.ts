import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { VerifierProfileComponent } from './verifier-profile.component';
import { VerifierProfileService } from '../../../core/services/verifier-profile.service';
import { AuthService } from '../../../core/auth/auth.service';
import {
  VerifierProfileResponse,
  VerifierSession,
  VerifierLoginHistoryItem,
  VerifierActivity
} from '../../../core/models/verifier-profile.models';

describe('VerifierProfileComponent', () => {
  let component: VerifierProfileComponent;
  let fixture: ComponentFixture<VerifierProfileComponent>;
  let mockProfileService: jasmine.SpyObj<VerifierProfileService>;
  let mockAuthService: jasmine.SpyObj<AuthService>;

  const mockProfileResponse: VerifierProfileResponse = {
    profile: {
      userId: '11111111-1111-1111-1111-111111111111',
      verifierId: 'VER-02819',
      fullName: 'Rahul Sharma',
      email: 'verifier@veripass.in',
      role: 'Compliance Officer',
      organization: 'ABC Compliance Services',
      status: 'ACTIVE'
    },
    scope: {
      regions: ['Uttar Pradesh', 'Delhi', 'Haryana'],
      verificationCategories: ['GST', 'PAN', 'Business Identity', 'Address', 'Certifications']
    },
    security: {
      mfaEnabled: true,
      lastLoginAtUtc: '2026-10-15T04:37:00Z',
      activeSessionCount: 2
    },
    activity: {
      range: '30d',
      casesReviewed: 1248,
      approved: 1032,
      rejected: 142,
      escalated: 74,
      averageReviewTimeSeconds: 492
    }
  };

  const mockSessions: VerifierSession[] = [
    {
      id: '22222222-2222-2222-2222-222222222222',
      device: 'Workstation Desktop',
      browser: 'Chrome 128',
      platform: 'Windows 11',
      ipAddress: '10.240.12.84',
      isCurrent: true,
      lastActiveAtUtc: '2026-10-15T04:37:00Z',
      createdAtUtc: '2026-10-15T01:00:00Z'
    },
    {
      id: '33333333-3333-3333-3333-333333333333',
      device: 'Mobile Terminal',
      browser: 'Safari',
      platform: 'iOS 18',
      ipAddress: '10.240.12.91',
      isCurrent: false,
      lastActiveAtUtc: '2026-10-15T03:30:00Z',
      createdAtUtc: '2026-10-14T10:00:00Z'
    }
  ];

  const mockLoginHistory: VerifierLoginHistoryItem[] = [
    {
      id: '44444444-4444-4444-4444-444444444444',
      timestampUtc: '2026-10-15T04:37:00Z',
      action: 'USER_LOGIN',
      status: 'SUCCESS',
      ipAddress: '10.240.12.84',
      browser: 'Chrome 128',
      platform: 'Windows 11'
    },
    {
      id: '55555555-5555-5555-5555-555555555555',
      timestampUtc: '2026-10-14T13:12:00Z',
      action: 'USER_LOGIN',
      status: 'SUCCESS',
      ipAddress: '10.240.12.84',
      browser: 'Chrome 128',
      platform: 'Windows 11'
    }
  ];

  beforeEach(async () => {
    mockProfileService = jasmine.createSpyObj<VerifierProfileService>('VerifierProfileService', [
      'getProfile',
      'getActivity',
      'getSessions',
      'revokeSession',
      'revokeOtherSessions',
      'getLoginHistory',
      'toggleMfa'
    ]);

    mockAuthService = jasmine.createSpyObj<AuthService>('AuthService', ['logout']);

    mockProfileService.getProfile.and.returnValue(of(mockProfileResponse));
    mockProfileService.getSessions.and.returnValue(of(mockSessions));
    mockProfileService.getLoginHistory.and.returnValue(of(mockLoginHistory));
    mockProfileService.revokeSession.and.returnValue(of({ success: true, message: 'Session revoked' }));
    mockProfileService.revokeOtherSessions.and.returnValue(of({ success: true, message: 'Revoked 1 session(s)', revokedCount: 1 }));
    mockProfileService.toggleMfa.and.returnValue(of({ success: true, mfaEnabled: false, message: 'MFA disabled' }));

    await TestBed.configureTestingModule({
      imports: [VerifierProfileComponent],
      providers: [
        provideRouter([]),
        { provide: VerifierProfileService, useValue: mockProfileService },
        { provide: AuthService, useValue: mockAuthService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(VerifierProfileComponent);
    component = fixture.componentInstance;
  });

  it('should create the component', () => {
    expect(component).toBeTruthy();
  });

  describe('1. Profile Loading and Display', () => {
    it('should fetch and render authoritative profile details on init', () => {
      fixture.detectChanges();

      expect(mockProfileService.getProfile).toHaveBeenCalledWith('30d');
      expect(component.profileData()).toEqual(mockProfileResponse);
      expect(component.loading()).toBeFalse();
      expect(component.userInitials()).toBe('RS');

      const el: HTMLElement = fixture.nativeElement;
      expect(el.querySelector('.user-fullname')?.textContent).toContain('Rahul Sharma');
      expect(el.querySelector('.user-role-text')?.textContent).toContain('Compliance Officer');
      expect(el.querySelector('.user-org-text')?.textContent).toContain('ABC Compliance Services');
      expect(el.querySelector('.status-indicator-badge')?.textContent).toContain('ACTIVE');
    });

    it('should render assigned jurisdictional regions and categories', () => {
      fixture.detectChanges();

      const el: HTMLElement = fixture.nativeElement;
      const regions = el.querySelectorAll('.region-tag');
      expect(regions.length).toBe(3);
      expect(regions[0].textContent).toContain('Uttar Pradesh');

      const categories = el.querySelectorAll('.cat-chip');
      expect(categories.length).toBe(5);
      expect(categories[0].textContent).toContain('GST');
    });

    it('should format review time from seconds into readable minutes and seconds', () => {
      expect(component.formatReviewTime(492)).toBe('8m 12s');
      expect(component.formatReviewTime(60)).toBe('1m');
      expect(component.formatReviewTime(45)).toBe('45s');
      expect(component.formatReviewTime(0)).toBe('—');
    });

    it('should display error state when profile load fails', () => {
      mockProfileService.getProfile.and.returnValue(throwError(() => ({ error: { message: 'Database unreachable' } })));
      
      component.loadProfile();
      fixture.detectChanges();

      expect(component.loading()).toBeFalse();
      expect(component.error()).toBe('Database unreachable');

      const el: HTMLElement = fixture.nativeElement;
      expect(el.querySelector('.error-banner')).toBeTruthy();
      expect(el.querySelector('.error-desc')?.textContent).toContain('Database unreachable');
    });
  });

  describe('2. Refresh Action', () => {
    it('should refresh profile data and show toast', fakeAsync(() => {
      fixture.detectChanges();

      component.refresh();
      expect(mockProfileService.getProfile).toHaveBeenCalledTimes(2);

      expect(component.toast()?.message).toBe('Profile refreshed successfully.');
      tick(4000);
      expect(component.toast()).toBeNull();
    }));
  });

  describe('3. Operational Activity Date Range', () => {
    it('should request new activity when range is changed', () => {
      fixture.detectChanges();

      const newActivity: VerifierActivity = {
        range: '7d',
        casesReviewed: 310,
        approved: 280,
        rejected: 20,
        escalated: 10,
        averageReviewTimeSeconds: 410
      };
      mockProfileService.getActivity.and.returnValue(of(newActivity));

      component.onRangeChange('7d');

      expect(mockProfileService.getActivity).toHaveBeenCalledWith('7d');
      expect(component.selectedRange()).toBe('7d');
      expect(component.profileData()?.activity.casesReviewed).toBe(310);
      expect(component.profileData()?.activity.approved).toBe(280);
    });

    it('should handle partial activity load failure gracefully', () => {
      fixture.detectChanges();
      mockProfileService.getActivity.and.returnValue(throwError(() => new Error('Service down')));

      component.onRangeChange('90d');

      expect(component.activityError()).toBe('Could not load activity for the selected range.');
      // The rest of the profile data remains intact
      expect(component.profileData()?.profile.fullName).toBe('Rahul Sharma');
    });
  });

  describe('4. Copy to Clipboard', () => {
    it('should copy email and verifier ID with user feedback', fakeAsync(() => {
      spyOn(navigator.clipboard, 'writeText').and.returnValue(Promise.resolve());

      component.copyText('verifier@veripass.in', 'email', 'Email');
      tick();

      expect(navigator.clipboard.writeText).toHaveBeenCalledWith('verifier@veripass.in');
      expect(component.copiedField()).toBe('email');
      expect(component.toast()?.message).toBe('Email copied to clipboard');

      tick(2000);
      expect(component.copiedField()).toBeNull();
    }));
  });

  describe('5. Session Management', () => {
    it('should open sessions modal and load active sessions', () => {
      component.openSessionsModal();

      expect(component.showSessionsModal()).toBeTrue();
      expect(mockProfileService.getSessions).toHaveBeenCalled();
      expect(component.sessionsList().length).toBe(2);
    });

    it('should revoke a single other session and decrement active session count', () => {
      fixture.detectChanges();
      component.sessionsList.set(mockSessions);

      const otherSession = mockSessions[1];
      component.revokeSingleSession(otherSession);

      expect(mockProfileService.revokeSession).toHaveBeenCalledWith(otherSession.id);
      expect(component.sessionsList().find(s => s.id === otherSession.id)).toBeUndefined();
      expect(component.profileData()?.security.activeSessionCount).toBe(1);
    });

    it('should prompt and confirm sign out of other sessions', fakeAsync(() => {
      fixture.detectChanges();
      component.promptSignOutOthers();
      expect(component.showRevokeConfirmModal()).toBeTrue();

      component.confirmSignOutOthers();
      tick();

      expect(mockProfileService.revokeOtherSessions).toHaveBeenCalled();
      expect(component.profileData()?.security.activeSessionCount).toBe(1);
      expect(component.showRevokeConfirmModal()).toBeFalse();
    }));
  });

  describe('6. Login History Drawer', () => {
    it('should open drawer and load history records', () => {
      component.openLoginHistoryDrawer();

      expect(component.showLoginHistoryDrawer()).toBeTrue();
      expect(mockProfileService.getLoginHistory).toHaveBeenCalled();
      expect(component.loginHistoryList().length).toBe(2);
    });

    it('should close drawer when requested', () => {
      component.openLoginHistoryDrawer();
      expect(component.showLoginHistoryDrawer()).toBeTrue();

      component.closeLoginHistoryDrawer();
      expect(component.showLoginHistoryDrawer()).toBeFalse();
    });
  });

  describe('7. MFA Configuration', () => {
    it('should open MFA modal and toggle state', () => {
      fixture.detectChanges();
      component.openMfaModal();
      expect(component.showMfaModal()).toBeTrue();

      component.toggleMfa(false);

      expect(mockProfileService.toggleMfa).toHaveBeenCalledWith(false);
      expect(component.profileData()?.security.mfaEnabled).toBeFalse();
      expect(component.showMfaModal()).toBeFalse();
    });
  });
});
