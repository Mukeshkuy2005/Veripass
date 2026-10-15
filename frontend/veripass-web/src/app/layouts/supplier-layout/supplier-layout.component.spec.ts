import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SupplierLayoutComponent } from './supplier-layout.component';
import { RouterTestingModule } from '@angular/router/testing';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/auth/auth.service';
import { BusinessService } from '../../core/services/business.service';
import { of, throwError } from 'rxjs';
import { signal } from '@angular/core';

describe('SupplierLayoutComponent', () => {
  let component: SupplierLayoutComponent;
  let fixture: ComponentFixture<SupplierLayoutComponent>;
  let apiMock: any;
  let authMock: any;
  let businessMock: any;

  beforeEach(async () => {
    localStorage.clear();

    apiMock = {
      get: jasmine.createSpy('get').and.returnValue(of([]))
    };

    authMock = {
      currentUser: signal({
        id: 'user-1',
        email: 'supplier@example.com',
        firstName: 'Aarav',
        lastName: 'Sharma',
        role: 'SUPPLIER'
      }),
      currentBusinessId: signal('biz-123'),
      logout: jasmine.createSpy('logout')
    };

    businessMock = {
      currentProfile: signal({
        id: 'biz-123',
        legalName: 'Acme Bharat Technologies Pvt Ltd',
        verificationStatus: 'VERIFIED',
        trustScore: 94
      }),
      pendingBuyerRequestsCount: signal(2)
    };

    await TestBed.configureTestingModule({
      imports: [SupplierLayoutComponent, RouterTestingModule],
      providers: [
        { provide: ApiService, useValue: apiMock },
        { provide: AuthService, useValue: authMock },
        { provide: BusinessService, useValue: businessMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SupplierLayoutComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => {
    localStorage.clear();
  });

  describe('Sidebar State & Persistence', () => {
    it('initializes sidebar as expanded by default when localStorage is empty', () => {
      expect(component.isSidebarCollapsed()).toBeFalse();
    });

    it('persists desktop sidebar collapsed state to localStorage on toggle', () => {
      // Simulate desktop viewport
      spyOnProperty(window, 'innerWidth', 'get').and.returnValue(1280);

      component.toggleSidebar();
      expect(component.isSidebarCollapsed()).toBeTrue();
      expect(localStorage.getItem('veripass_supplier_sidebar_collapsed')).toBe('true');

      component.toggleSidebar();
      expect(component.isSidebarCollapsed()).toBeFalse();
      expect(localStorage.getItem('veripass_supplier_sidebar_collapsed')).toBe('false');
    });

    it('initializes sidebar as collapsed when localStorage has true', () => {
      localStorage.setItem('veripass_supplier_sidebar_collapsed', 'true');
      const newFixture = TestBed.createComponent(SupplierLayoutComponent);
      const newComp = newFixture.componentInstance;
      expect(newComp.isSidebarCollapsed()).toBeTrue();
    });

    it('toggles mobile drawer instead of desktop sidebar when viewport <= 1024', () => {
      spyOnProperty(window, 'innerWidth', 'get').and.returnValue(768);

      expect(component.mobileDrawerOpen()).toBeFalse();
      component.toggleSidebar();
      expect(component.mobileDrawerOpen()).toBeTrue();
      expect(component.isSidebarCollapsed()).toBeFalse(); // desktop state untouched
    });

    it('closes mobile drawer on navigation click in mobile viewport', () => {
      spyOnProperty(window, 'innerWidth', 'get').and.returnValue(768);
      component.mobileDrawerOpen.set(true);

      component.onNavClick();
      expect(component.mobileDrawerOpen()).toBeFalse();
    });

    it('closes mobile drawer and dropdown when Escape key is pressed', () => {
      component.mobileDrawerOpen.set(true);
      component.notificationDropdownOpen.set(true);

      component.onEscape();

      expect(component.mobileDrawerOpen()).toBeFalse();
      expect(component.notificationDropdownOpen()).toBeFalse();
    });
  });

  describe('Notification Indicator & Panel States', () => {
    it('does not render red alert dot when unread count is 0', () => {
      component.unreadCount.set(0);
      fixture.detectChanges();

      const dot = fixture.nativeElement.querySelector('.alert-dot');
      expect(dot).toBeNull();
    });

    it('renders red alert dot when unread count is greater than 0', () => {
      component.unreadCount.set(3);
      fixture.detectChanges();

      const dot = fixture.nativeElement.querySelector('.alert-dot');
      expect(dot).not.toBeNull();
    });

    it('correctly calculates unread count from API notifications feed', () => {
      const mockNotifs = [
        { id: '1', title: 'GSTIN Verified', isRead: true },
        { id: '2', title: 'New Buyer RFQ', isRead: false },
        { id: '3', title: 'Audit Alert', read: false }
      ];
      apiMock.get.and.returnValue(of(mockNotifs));

      component.loadUnreadNotifications();

      expect(component.unreadCount()).toBe(2);
      expect(component.notificationsList().length).toBe(3);
      expect(component.notificationsError()).toBeFalse();
    });

    it('handles notification API error gracefully', () => {
      apiMock.get.and.returnValue(throwError(() => new Error('Server error')));

      component.loadUnreadNotifications();

      expect(component.notificationsError()).toBeTrue();
      expect(component.unreadCount()).toBe(0);
      expect(component.notificationsList().length).toBe(0);
    });

    it('renders empty state in dropdown when there are no notifications', () => {
      component.notificationsLoading.set(false);
      component.notificationsError.set(false);
      component.notificationsList.set([]);
      component.notificationDropdownOpen.set(true);
      fixture.detectChanges();

      const emptyText = fixture.nativeElement.querySelector('.notif-empty-state .empty-title');
      expect(emptyText?.textContent).toContain('No notifications');
    });
  });

  describe('Search Input Alignment & Attributes', () => {
    it('has accessible label and 24x24 outline SVG icon on search input', () => {
      const searchInput = fixture.nativeElement.querySelector('.search-input');
      expect(searchInput).not.toBeNull();
      expect(searchInput.getAttribute('aria-label')).toBeTruthy();

      const searchSvg = fixture.nativeElement.querySelector('.search-icon');
      expect(searchSvg).not.toBeNull();
      expect(searchSvg.getAttribute('viewBox')).toBe('0 0 24 24');
    });
  });
});
