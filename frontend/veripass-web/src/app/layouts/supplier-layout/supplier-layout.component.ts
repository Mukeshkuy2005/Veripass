import { Component, computed, inject, OnInit, signal, HostListener, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { BusinessService } from '../../core/services/business.service';
import { ApiService } from '../../core/services/api.service';

@Component({
  selector: 'app-supplier-layout',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  templateUrl: './supplier-layout.component.html',
  styleUrl: './supplier-layout.component.css'
})
export class SupplierLayoutComponent implements OnInit {
  private router = inject(Router);
  private api = inject(ApiService);
  private elementRef = inject(ElementRef);
  public auth = inject(AuthService);
  public businessService = inject(BusinessService);

  private readonly SIDEBAR_COLLAPSED_KEY = 'veripass_supplier_sidebar_collapsed';

  isSidebarCollapsed = signal<boolean>(this.getInitialSidebarCollapsed());
  mobileDrawerOpen = signal<boolean>(false);
  userMenuOpen = signal<boolean>(false);
  searchQuery = signal<string>('');

  // Real Notifications State
  unreadCount = signal<number>(0);
  notificationsLoading = signal<boolean>(false);
  notificationsError = signal<boolean>(false);
  notificationsList = signal<any[]>([]);
  notificationDropdownOpen = signal<boolean>(false);

  user = this.auth.currentUser;

  userName = computed(() => {
    const u = this.user();
    if (!u) return 'Authorized User';
    const fullName = `${u.firstName || ''} ${u.lastName || ''}`.trim();
    return fullName || u.email || 'Authorized User';
  });

  userInitials = computed(() => {
    const u = this.user();
    if (!u) return 'BA';
    const first = (u.firstName || '')[0] || '';
    const last = (u.lastName || '')[0] || '';
    const inits = (first + last).toUpperCase();
    return inits || (u.email ? u.email[0].toUpperCase() : 'BA');
  });

  companyName = computed(() => {
    return this.businessService.currentProfile()?.legalName
      || this.businessService.currentProfile()?.tradeName
      || this.user()?.companyName
      || this.user()?.organizationName
      || 'My Business';
  });

  verificationStatus = computed(() => {
    return this.businessService.currentProfile()?.verificationStatus || 'UNVERIFIED';
  });

  pendingBuyerRequestsCount = this.businessService.pendingBuyerRequestsCount;

  currentPageTitle = computed(() => {
    const url = this.router.url;
    if (url.includes('/supplier/verification')) return 'Verification';
    if (url.includes('/supplier/documents')) return 'Credentials & Documents';
    if (url.includes('/supplier/passport')) return 'Business Passport';
    if (url.includes('/supplier/compliance/calendar')) return 'Compliance Calendar';
    if (url.includes('/supplier/compliance')) return 'Compliance';
    if (url.includes('/supplier/settings')) return 'Settings';
    if (url.includes('/supplier/history')) return 'Verification History';
    if (url.includes('/supplier/requests') || url.includes('/supplier/buyer-requests')) return 'Buyer Requests';
    return 'Dashboard';
  });

  ngOnInit(): void {
    this.loadUnreadNotifications();
  }

  private getInitialSidebarCollapsed(): boolean {
    if (typeof window !== 'undefined' && window.localStorage) {
      try {
        return localStorage.getItem(this.SIDEBAR_COLLAPSED_KEY) === 'true';
      } catch {
        return false;
      }
    }
    return false;
  }

  toggleSidebar(): void {
    if (typeof window !== 'undefined' && window.innerWidth <= 1024) {
      this.mobileDrawerOpen.update(v => !v);
    } else {
      this.isSidebarCollapsed.update(collapsed => {
        const next = !collapsed;
        if (typeof window !== 'undefined' && window.localStorage) {
          try {
            localStorage.setItem(this.SIDEBAR_COLLAPSED_KEY, String(next));
          } catch (e) {
            console.error('Failed to save sidebar state to localStorage', e);
          }
        }
        return next;
      });
    }
  }

  onNavClick(): void {
    // Only close the overlay drawer on mobile viewports; keep desktop sidebar intact
    if (typeof window !== 'undefined' && window.innerWidth <= 1024) {
      this.mobileDrawerOpen.set(false);
    }
  }

  closeMobileDrawer(): void {
    this.mobileDrawerOpen.set(false);
  }

  toggleUserMenu(): void {
    this.userMenuOpen.update(v => !v);
  }

  closeUserMenu(): void {
    this.userMenuOpen.set(false);
  }

  toggleNotificationDropdown(): void {
    const next = !this.notificationDropdownOpen();
    this.notificationDropdownOpen.set(next);
    if (next) {
      this.loadUnreadNotifications();
    }
  }

  closeNotificationDropdown(): void {
    this.notificationDropdownOpen.set(false);
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.notificationDropdownOpen()) {
      const container = this.elementRef.nativeElement.querySelector('.notifications-menu-container');
      if (container && !container.contains(event.target as Node)) {
        this.closeNotificationDropdown();
      }
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.notificationDropdownOpen()) {
      this.closeNotificationDropdown();
    }
    if (this.mobileDrawerOpen()) {
      this.closeMobileDrawer();
    }
  }

  @HostListener('window:resize')
  onWindowResize(): void {
    if (typeof window !== 'undefined' && window.innerWidth > 1024 && this.mobileDrawerOpen()) {
      this.mobileDrawerOpen.set(false);
    }
  }

  loadUnreadNotifications(): void {
    this.notificationsLoading.set(true);
    this.notificationsError.set(false);
    this.api.get<any[]>('notifications').subscribe({
      next: (items) => {
        this.notificationsLoading.set(false);
        if (Array.isArray(items)) {
          this.notificationsList.set(items);
          const unread = items.filter(n => !n.isRead && !n.read).length;
          this.unreadCount.set(unread);
        } else {
          this.notificationsList.set([]);
          this.unreadCount.set(0);
        }
      },
      error: () => {
        this.notificationsLoading.set(false);
        this.notificationsError.set(true);
        this.notificationsList.set([]);
        this.unreadCount.set(0);
      }
    });
  }

  onSearch(): void {
    const q = this.searchQuery().trim();
    if (q) {
      this.router.navigate(['/supplier/documents'], { queryParams: { q } });
    }
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/auth/login']);
  }
}
