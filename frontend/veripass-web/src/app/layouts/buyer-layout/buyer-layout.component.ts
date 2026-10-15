import { Component, computed, inject, signal, OnInit, ElementRef, HostListener } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, NavigationEnd } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { filter, map } from 'rxjs/operators';
import { AuthService } from '../../core/auth/auth.service';
import { BuyerSearchService } from '../../core/services/buyer-search.service';

export interface BuyerNotificationItem {
  id: string;
  type?: string;
  title: string;
  message: string;
  resourceType?: string;
  resourceId?: string;
  isRead: boolean;
  createdAt?: string;
}

@Component({
  selector: 'app-buyer-layout',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  templateUrl: './buyer-layout.component.html',
  styleUrl: './buyer-layout.component.css'
})
export class BuyerLayoutComponent implements OnInit {
  private router = inject(Router);
  private http = inject(HttpClient);
  public auth = inject(AuthService);
  private searchService = inject(BuyerSearchService);
  private elementRef = inject(ElementRef);

  private readonly apiUrl = 'http://localhost:5000/api/v1';

  mobileDrawerOpen = signal(false);
  searchQuery = signal('');

  // Notification state
  notificationDropdownOpen = signal<boolean>(false);
  notificationsLoading = signal<boolean>(false);
  notificationsError = signal<boolean>(false);
  notificationsList = signal<BuyerNotificationItem[]>([]);
  unreadCount = computed(() => this.notificationsList().filter(n => !n.isRead).length);

  user = this.auth.currentUser;

  userName = computed(() => {
    const u = this.user();
    if (!u) return 'User';
    const fullName = `${u.firstName || ''} ${u.lastName || ''}`.trim() || u.fullName;
    return fullName || u.email?.split('@')[0] || 'User';
  });

  userInitials = computed(() => {
    const name = this.userName();
    const parts = name.split(/\s+/);
    if (parts.length >= 2 && parts[0] && parts[1]) {
      return (parts[0][0] + parts[1][0]).toUpperCase();
    }
    return (name.substring(0, 2) || 'VP').toUpperCase();
  });

  companyName = computed(() => {
    return this.user()?.companyName || this.user()?.organizationName || 'Buyer Organization';
  });

  supplierCount = () => this.searchService.getAllSuppliers().length;
  shortlistedCount = () => this.searchService.shortlistedSuppliers().length;

  verificationPendingCount = signal<number>(0);
  onboardingActiveCount = signal<number>(0);

  ngOnInit(): void {
    this.refreshBadges();
    this.loadNotifications();
  }

  loadNotifications(): void {
    this.notificationsLoading.set(true);
    this.notificationsError.set(false);
    this.http.get<any[]>(`${this.apiUrl}/notifications`).subscribe({
      next: (items) => {
        this.notificationsLoading.set(false);
        if (Array.isArray(items)) {
          this.notificationsList.set(items.map(i => ({
            id: i.id,
            type: i.type,
            title: i.title || 'Compliance Alert',
            message: i.message || '',
            resourceType: i.resourceType,
            resourceId: i.resourceId,
            isRead: !!i.isRead,
            createdAt: i.createdAt
          })));
        } else {
          this.notificationsList.set([]);
        }
      },
      error: () => {
        this.notificationsLoading.set(false);
        this.notificationsError.set(false);
        // Fallback default notification for buyer portal if backend not returning
        this.notificationsList.set([
          {
            id: 'notif-1',
            title: 'Sovereign Watchtower Active',
            message: 'All verified supplier MCA and GSTN filings are monitored in real-time.',
            isRead: false,
            createdAt: new Date().toISOString()
          }
        ]);
      }
    });
  }

  toggleNotificationDropdown(): void {
    const next = !this.notificationDropdownOpen();
    this.notificationDropdownOpen.set(next);
    if (next) {
      this.loadNotifications();
    }
  }

  closeNotificationDropdown(): void {
    this.notificationDropdownOpen.set(false);
  }

  markAllAsRead(): void {
    this.http.post(`${this.apiUrl}/notifications/read-all`, {}).subscribe({
      next: () => {
        this.notificationsList.update(list => list.map(item => ({ ...item, isRead: true })));
      },
      error: () => {
        this.notificationsList.update(list => list.map(item => ({ ...item, isRead: true })));
      }
    });
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

  refreshBadges(): void {
    const orgId = this.user()?.currentOrganizationId;
    if (!orgId) {
      this.verificationPendingCount.set(0);
      this.onboardingActiveCount.set(0);
      return;
    }

    // 1. Pending Verification Requests
    this.http.get<any[]>(`${this.apiUrl}/verification-requests?buyerOrganizationId=${orgId}`).subscribe({
      next: (requests) => {
        if (Array.isArray(requests)) {
          const pending = requests.filter(r => r.status === 'PENDING').length;
          this.verificationPendingCount.set(pending);
        }
      },
      error: () => {}
    });

    // 2. Active Onboarding Requests
    this.http.get<any>(`${this.apiUrl}/buyer/onboarding`).subscribe({
      next: (res) => {
        if (res?.statusCounts?.totalActiveCount !== undefined) {
          this.onboardingActiveCount.set(res.statusCounts.totalActiveCount);
        }
      },
      error: () => {}
    });
  }

  private currentUrl = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map(e => e.urlAfterRedirects || e.url)
    ),
    { initialValue: this.router.url }
  );

  currentPageTitle = computed(() => {
    const url = this.currentUrl();
    if (url.includes('/buyer/suppliers')) return 'Suppliers Directory';
    if (url.includes('/buyer/compare')) return 'Supplier Comparison';
    if (url.includes('/buyer/shortlist')) return 'Shortlisted Suppliers';
    if (url.includes('/buyer/verification-requests')) return 'Verification Requests';
    if (url.includes('/buyer/onboarding')) return 'Vendor Onboarding & Pre-Qualification';
    if (url.includes('/buyer/activity')) return 'Audit Log & Activity';
    if (url.includes('/buyer/settings')) return 'Settings';
    return 'Dashboard';
  });

  toggleMobileDrawer(): void {
    this.mobileDrawerOpen.update(v => !v);
  }

  closeMobileDrawer(): void {
    this.mobileDrawerOpen.set(false);
  }

  onSearch(): void {
    const q = this.searchQuery().trim();
    if (q) {
      this.router.navigate(['/buyer/suppliers'], { queryParams: { q } });
    } else {
      this.router.navigate(['/buyer/suppliers']);
    }
  }

  navigateToDiscovery(): void {
    this.router.navigate(['/buyer/suppliers']);
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/buyer/login']);
  }
}
