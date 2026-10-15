import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { BusinessService } from '../../../core/services/business.service';
import { formatIstDateTime } from '../../../core/utils/ist-date-formatter';

export interface HistoryEvent {
  id: string;
  dateStr: string;
  timeStr: string;
  title: string;
  category: 'Identity' | 'Documents' | 'Compliance';
  status: 'VERIFIED' | 'FLAGGED' | 'UPLOADED';
  source: string;
  credential: string;
  performedBy: string;
  prevStatus: string;
  newStatus: string;
}

@Component({
  selector: 'app-verification-history',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './verification-history.component.html',
  styleUrl: './verification-history.component.css'
})
export class VerificationHistoryComponent implements OnInit {
  private businessService = inject(BusinessService);

  tabs = ['All', 'Identity', 'Documents', 'Compliance'];
  selectedTab = signal('All');
  selectedEvent = signal<HistoryEvent | null>(null);
  searchQuery = signal('');
  isRefreshing = signal(false);

  events = signal<HistoryEvent[]>([]);

  totalCount = computed(() => this.events().length);
  verifiedCount = computed(() => this.events().filter(e => e.status === 'VERIFIED').length);
  flaggedCount = computed(() => this.events().filter(e => e.status === 'FLAGGED').length);
  uploadedCount = computed(() => this.events().filter(e => e.status === 'UPLOADED').length);

  filteredEvents = computed(() => {
    const t = this.selectedTab();
    const query = this.searchQuery().toLowerCase().trim();
    let result = t === 'All' ? this.events() : this.events().filter(e => e.category === t);
    if (query) {
      result = result.filter(e =>
        e.title.toLowerCase().includes(query) ||
        e.id.toLowerCase().includes(query) ||
        e.credential.toLowerCase().includes(query) ||
        e.source.toLowerCase().includes(query) ||
        e.newStatus.toLowerCase().includes(query)
      );
    }
    return result;
  });

  ngOnInit(): void {
    const profile = this.businessService.currentProfile();
    if (profile?.id) {
      this.loadRealEvents(profile.id, profile);
    }
  }

  private loadRealEvents(businessId: string, profile: any): void {
    this.businessService.getComplianceEvents(businessId).subscribe({
      next: (compEvents) => {
        if (compEvents && compEvents.length > 0) {
          const mapped: HistoryEvent[] = compEvents.map((ce, idx) => {
            const dt = formatIstDateTime(ce.createdAt);
            return {
              id: `VE-${ce.id ? ce.id.substring(0, 8).toUpperCase() : (idx + 1).toString().padStart(3, '0')}`,
              dateStr: dt.datePart,
              timeStr: dt.timePart,
              title: ce.title || ce.eventType || 'Compliance Event',
              category: (ce.eventType?.includes('Identity') || ce.eventType?.includes('GST') || ce.eventType?.includes('PAN')) ? 'Identity' : (ce.eventType?.includes('Doc') ? 'Documents' : 'Compliance'),
              status: ce.severity === 'Critical' ? 'FLAGGED' : 'VERIFIED',
              source: 'Verification Ledger',
              credential: profile?.gstin || profile?.pan || 'Statutory Filing',
              performedBy: 'Verification Engine',
              prevStatus: 'PENDING',
              newStatus: ce.severity === 'Critical' ? 'FLAGGED' : 'VERIFIED'
            };
          });
          this.events.set(mapped);
        } else {
          this.events.set([]);
        }
      },
      error: () => this.events.set([])
    });
  }

  getTabCount(tab: string): number {
    if (tab === 'All') return this.events().length;
    return this.events().filter(e => e.category === tab).length;
  }

  refreshHistory(): void {
    this.isRefreshing.set(true);
    const profile = this.businessService.currentProfile();
    if (profile?.id) {
      this.loadRealEvents(profile.id, profile);
    }
    setTimeout(() => {
      this.isRefreshing.set(false);
    }, 350);
  }

  selectEvent(ev: HistoryEvent): void {
    this.selectedEvent.set(ev);
  }
}
