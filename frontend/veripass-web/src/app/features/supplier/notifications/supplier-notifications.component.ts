import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';

interface NotificationItem {
  id: string;
  type: 'Compliance' | 'Verification' | 'Buyer Requests' | 'System';
  icon: string;
  iconColor: 'red' | 'green' | 'blue' | 'purple';
  title: string;
  desc: string;
  timeStr: string;
  read: boolean;
  actionRequired?: string;
  expiryDate?: string;
}

@Component({
  selector: 'app-supplier-notifications',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './supplier-notifications.component.html',
  styleUrl: './supplier-notifications.component.css'
})
export class SupplierNotificationsComponent implements OnInit {
  private api = inject(ApiService);

  tabs = ['All', 'Verification', 'Compliance', 'Buyer Requests', 'System'];
  selectedTab = signal('All');
  selectedNotification = signal<NotificationItem | null>(null);

  notifications = signal<NotificationItem[]>([]);
  loading = signal<boolean>(false);
  error = signal<boolean>(false);

  ngOnInit(): void {
    this.loadNotifications();
  }

  loadNotifications(): void {
    this.loading.set(true);
    this.error.set(false);
    this.api.get<any[]>('notifications').subscribe({
      next: (notifs) => {
        this.loading.set(false);
        if (Array.isArray(notifs) && notifs.length > 0) {
          const mapped: NotificationItem[] = notifs.map(n => ({
            id: n.id || `notif-${Math.random().toString(36).substring(2, 9)}`,
            type: n.type || 'System',
            icon: n.type === 'Compliance' ? '🔴' : (n.type === 'Verification' ? '✓' : (n.type === 'Buyer Requests' ? '👤' : '🛡️')),
            iconColor: n.type === 'Compliance' ? 'red' : (n.type === 'Verification' ? 'green' : (n.type === 'Buyer Requests' ? 'blue' : 'purple')),
            title: n.title || 'Notification',
            desc: n.message || n.desc || '',
            timeStr: n.createdAt ? new Date(n.createdAt).toLocaleDateString() : 'Recent',
            read: !!(n.isRead || n.read),
            actionRequired: n.actionRequired,
            expiryDate: n.expiryDate
          }));
          this.notifications.set(mapped);
        } else {
          this.notifications.set([]);
        }
      },
      error: () => {
        this.loading.set(false);
        this.error.set(true);
        this.notifications.set([]);
      }
    });
  }

  filteredNotifications = computed(() => {
    const t = this.selectedTab();
    const list = this.notifications();
    if (t === 'All') return list;
    return list.filter(n => n.type === t);
  });

  selectNotification(item: NotificationItem): void {
    if (!item.read) {
      item.read = true;
      this.api.post(`notifications/${item.id}/read`, {}).subscribe({ error: () => {} });
    }
    this.selectedNotification.set(item);
  }

  markAllAsRead(): void {
    this.notifications.update(list => list.map(n => ({ ...n, read: true })));
    this.api.post('notifications/read-all', {}).subscribe({ error: () => {} });
  }
}
