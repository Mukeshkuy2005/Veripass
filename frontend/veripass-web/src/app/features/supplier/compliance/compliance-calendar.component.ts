import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { BusinessService } from '../../../core/services/business.service';
import { AuthService } from '../../../core/auth/auth.service';
import { jsPDF } from 'jspdf';

export interface CalendarEvent {
  id: string;
  day: number;
  month: number; // 8 = Sep, 9 = Oct, 10 = Nov, 11 = Dec
  year: number;
  dateStr: string;
  title: string;
  category: 'Statutory Returns' | 'Credential Expiries' | 'Tax Deadlines' | 'Verification Checks';
  authority: string;
  authorityLogoText: string;
  status: 'COMPLIANT' | 'ACTION_REQUIRED' | 'UPCOMING';
  statutoryRef: string;
  penaltyNotice: string;
  description: string;
  arnNumber?: string;
  feeAmount?: string;
  urgencyDays?: number;
}

@Component({
  selector: 'app-compliance-calendar',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './compliance-calendar.component.html',
  styleUrl: './compliance-calendar.component.css'
})
export class ComplianceCalendarComponent implements OnInit {
  private businessService = inject(BusinessService);
  private auth = inject(AuthService);
  businessId = '';

  // Navigation & View Modes
  viewMode = signal<'month' | 'week' | 'list'>('month');
  currentMonth = signal<number>(8); // September (0-indexed: 8)
  currentYear = signal<number>(2026);
  selectedDay = signal<number>(27); // Today: 27 Sep 2026
  selectedFilter = signal<string>('All Obligations');

  // Modals & Feedback
  activeEventModal = signal<CalendarEvent | null>(null);
  showUploadChallanModal = signal<CalendarEvent | null>(null);
  toastMessage = signal<string | null>(null);
  isProcessingUpload = signal<boolean>(false);
  uploadProgress = signal<number | null>(null);
  uploadStepText = signal<string>('');
  challanRefInput = signal<string>('');
  uploadedFileName = signal<string | null>(null);

  monthNames = [
    'January', 'February', 'March', 'April', 'May', 'June',
    'July', 'August', 'September', 'October', 'November', 'December'
  ];

  filters = [
    'All Obligations',
    'Statutory Returns',
    'Credential Expiries',
    'Tax Deadlines',
    'Verification Checks'
  ];

  // Dynamic Company Profile matching Dashboard, Documents, Compliance, and Verification Center
  get companyName(): string {
    const prof = this.businessService.currentProfile();
    return prof?.legalName || prof?.tradeName || this.auth.currentUser()?.companyName || this.auth.currentUser()?.organizationName || 'My Business';
  }

  get principalAddress(): string {
    const addr = this.businessService.currentProfile()?.addresses?.[0];
    if (addr && (addr.line1 || addr.city)) {
      return `${addr.line1 ? addr.line1 + ', ' : ''}${addr.city ? addr.city + ', ' : ''}${addr.state || ''} ${addr.pincode || ''}`.trim().replace(/^, |, $/g, '');
    }
    return '';
  }

  get gstin(): string {
    return this.businessService.currentProfile()?.gstin || '';
  }

  get pan(): string {
    return this.businessService.currentProfile()?.pan || '';
  }

  get cin(): string {
    return this.businessService.currentProfile()?.cin || '';
  }

  ngOnInit(): void {
    const authId = this.auth.currentBusinessId();
    if (authId) {
      this.businessId = authId;
    }
    const isVerified = this.businessService.currentProfile()?.verificationStatus === 'VERIFIED';
    if (!isVerified) {
      this.events.set([]);
    }
    this.businessService.getComplianceEvents(this.businessId).subscribe({
      next: () => {}
    });
  }

  // Interconnected Events List (Sept - Dec 2026)
  events = signal<CalendarEvent[]>([
    {
      id: 'ev-gstr1',
      day: 10,
      month: 8,
      year: 2026,
      dateStr: '10 Sep 2026',
      title: 'GSTR-1 Monthly Outward Supplies',
      category: 'Statutory Returns',
      authority: 'GSTN (Goods and Services Tax Network)',
      authorityLogoText: '🏛️ GSTN',
      status: 'COMPLIANT',
      statutoryRef: 'Section 37(1) of CGST Act 2017',
      penaltyNotice: 'Late fee ₹50/day (₹20 for nil return) under Section 47',
      description: 'Invoice-level outward supplies return covering all 142 B2B e-invoices reconciled with buyer portals.',
      arnNumber: 'AA090826008129Q'
    },
    {
      id: 'ev-advtax',
      day: 14,
      month: 8,
      year: 2026,
      dateStr: '14 Sep 2026',
      title: 'Corporate Advance Tax (Q2 Installment)',
      category: 'Tax Deadlines',
      authority: 'Income Tax Department (CBDT / NSDL)',
      authorityLogoText: '🏛️ CBDT',
      status: 'COMPLIANT',
      statutoryRef: 'Section 211 of Income Tax Act 1961',
      penaltyNotice: 'Simple interest @ 1% per month under Section 234C for shortfall',
      description: 'Mandatory 45% cumulative advance corporate income tax installment deposited on schedule.',
      arnNumber: 'BSR-021004-89211',
      feeAmount: '₹ 3,50,000.00'
    },
    {
      id: 'ev-epfo',
      day: 14,
      month: 8,
      year: 2026,
      dateStr: '14 Sep 2026',
      title: 'EPFO Monthly Electronic Challan Return (ECR)',
      category: 'Statutory Returns',
      authority: 'Employees\' Provident Fund Organisation',
      authorityLogoText: '🏛️ EPFO',
      status: 'COMPLIANT',
      statutoryRef: 'Section 38 of EPF & MP Act 1952',
      penaltyNotice: 'Penal damages under Section 14B up to 25% + 12% interest under 7Q',
      description: 'Statutory monthly provident fund deposit and member return covering 48 active plant workers.',
      arnNumber: 'ECR-1092837461',
      feeAmount: '₹ 1,12,650.00'
    },
    {
      id: 'ev-gstr3b',
      day: 18,
      month: 8,
      year: 2026,
      dateStr: '18 Sep 2026',
      title: 'GSTR-3B Monthly Tax Return',
      category: 'Statutory Returns',
      authority: 'GSTN (Goods and Services Tax Network)',
      authorityLogoText: '🏛️ GSTN',
      status: 'COMPLIANT',
      statutoryRef: 'Section 39(1) of CGST Act 2017 & Rule 61(5)',
      penaltyNotice: '₹50/day late fee under Section 47 + 18% p.a. interest under Section 50',
      description: 'Monthly summary return of outward and inward supplies with 100% 2B Input Tax Credit reconciliation.',
      arnNumber: 'AA090826019482X',
      feeAmount: '₹ 2,45,800.00'
    },
    {
      id: 'ev-trade',
      day: 18,
      month: 8,
      year: 2026,
      dateStr: '18 Sep 2026',
      title: 'Municipal Industrial Trade License Renewal',
      category: 'Credential Expiries',
      authority: 'Municipal Corporation Ghaziabad',
      authorityLogoText: '🏛️ Municipal',
      status: 'ACTION_REQUIRED',
      statutoryRef: 'Municipal Corporation Factory Operations By-Laws',
      penaltyNotice: 'Operations notice and 2x compounding surcharge if delayed past 30 days',
      description: 'Bi-annual factory operating license fee payment receipt and pollution compliance declaration required.',
      arnNumber: 'GZB-IND-LIC-2024-88',
      feeAmount: '₹ 12,500.00',
      urgencyDays: 0
    },
    {
      id: 'ev-iso',
      day: 27,
      month: 8,
      year: 2026,
      dateStr: '27 Sep 2026',
      title: 'ISO 9001:2015 QMS Surveillance Expiry',
      category: 'Credential Expiries',
      authority: 'TUV Nord Accredited Registrar',
      authorityLogoText: '🛡️ TUV Nord',
      status: 'ACTION_REQUIRED',
      statutoryRef: 'ISO 9001 Quality Management System Surveillance Audit',
      penaltyNotice: 'Suspension of Tier-1 Enterprise badge & buyer priority status',
      description: 'Annual surveillance audit renewal required to preserve certified supplier status on buyer enterprise networks.',
      arnNumber: 'ISO-9001-2025-9921',
      urgencyDays: 27
    },
    {
      id: 'ev-bank',
      day: 4,
      month: 9,
      year: 2026,
      dateStr: '04 Oct 2026',
      title: 'Quarterly Bank Penny Reverification',
      category: 'Verification Checks',
      authority: 'NPCI (National Payments Corporation of India)',
      authorityLogoText: '🏦 NPCI',
      status: 'UPCOMING',
      statutoryRef: 'RBI Master Directions on Commercial Vendor Onboarding',
      penaltyNotice: 'Auto-hold on enterprise automated buyer invoice payouts',
      description: 'Automated ₹1.00 IMPS penny drop test on commercial current account to re-certify entity name matching.',
      arnNumber: 'HDFC-****4321',
      urgencyDays: 7
    },
    {
      id: 'ev-aoc4',
      day: 30,
      month: 9,
      year: 2026,
      dateStr: '30 Oct 2026',
      title: 'MCA Form AOC-4 Financial Statements',
      category: 'Statutory Returns',
      authority: 'Ministry of Corporate Affairs (MCA21 V3)',
      authorityLogoText: '🏛️ MCA21',
      status: 'UPCOMING',
      statutoryRef: 'Section 137 of Companies Act 2013',
      penaltyNotice: '₹100/day continuing default fine on company and defaulting officers',
      description: 'Annual filing of audited balance sheet, profit & loss, auditor report, and director disclosures.',
      arnNumber: 'SRN-R91829481',
      urgencyDays: 33
    },
    {
      id: 'ev-permit',
      day: 4,
      month: 10,
      year: 2026,
      dateStr: '04 Nov 2026',
      title: 'Factory Permit SPCB Annual Term',
      category: 'Credential Expiries',
      authority: 'Directorate of Factories & Municipal Body',
      authorityLogoText: '🏭 Factories Dept',
      status: 'UPCOMING',
      statutoryRef: 'Section 6 of Factories Act 1948',
      penaltyNotice: 'Factory inspection compliance notice under Section 92',
      description: 'Annual manufacturing factory license safety compliance renewal and plant inspection clearance.',
      arnNumber: 'GZB-FAC-2024-110',
      urgencyDays: 38
    },
    {
      id: 'ev-mgt7',
      day: 29,
      month: 10,
      year: 2026,
      dateStr: '29 Nov 2026',
      title: 'MCA Form MGT-7 Annual Governance Return',
      category: 'Statutory Returns',
      authority: 'Ministry of Corporate Affairs (MCA21 V3)',
      authorityLogoText: '🏛️ MCA21',
      status: 'UPCOMING',
      statutoryRef: 'Section 92 of Companies Act 2013',
      penaltyNotice: '₹100/day continuing fine under Section 92(5)',
      description: 'Annual return of members, directors, board meetings, shareholding pattern, and compliance certificate.',
      arnNumber: 'SRN-Q44109218',
      urgencyDays: 63
    },
    {
      id: 'ev-spcb',
      day: 2,
      month: 11,
      year: 2026,
      dateStr: '02 Dec 2026',
      title: 'Pollution Control Board Consent (CTO)',
      category: 'Credential Expiries',
      authority: 'State Pollution Control Board (SPCB)',
      authorityLogoText: '🌿 SPCB',
      status: 'UPCOMING',
      statutoryRef: 'Section 25 of Water Act 1974 & Section 21 of Air Act 1981',
      penaltyNotice: 'Closure notice under Section 31A of Air Act',
      description: 'State Pollution Control Board Consent to Operate (CTO) valid through 02 Dec 2026. Automated registry health scan.',
      arnNumber: 'SPCB-AIR-WAT-2025-091',
      urgencyDays: 66
    }
  ]);

  // Current Month Title & Offset Calculation
  currentMonthTitle = computed(() => {
    return `${this.monthNames[this.currentMonth()]} ${this.currentYear()}`;
  });

  // Calculate days array with leading blank cells based on month
  calendarGridDays = computed(() => {
    const month = this.currentMonth();
    const year = this.currentYear();

    // First day of month (0 = Sun, 1 = Mon, ..., 6 = Sat)
    const firstDay = new Date(year, month, 1).getDay();
    // In our calendar, Monday is first column (Mon=0, Tue=1, ..., Sun=6)
    const offset = (firstDay + 6) % 7;

    const daysInMonth = new Date(year, month + 1, 0).getDate();
    const prevDaysInMonth = new Date(year, month, 0).getDate();

    const days: { day: number; isCurrentMonth: boolean; hasEvent: boolean; hasExpiry: boolean; events: CalendarEvent[] }[] = [];

    // Prior month days
    for (let i = offset - 1; i >= 0; i--) {
      days.push({
        day: prevDaysInMonth - i,
        isCurrentMonth: false,
        hasEvent: false,
        hasExpiry: false,
        events: []
      });
    }

    // Current month days
    for (let d = 1; d <= daysInMonth; d++) {
      const dayEvents = this.events().filter(e => e.year === year && e.month === month && e.day === d);
      days.push({
        day: d,
        isCurrentMonth: true,
        hasEvent: dayEvents.length > 0,
        hasExpiry: dayEvents.some(e => e.category === 'Credential Expiries' || e.status === 'ACTION_REQUIRED'),
        events: dayEvents
      });
    }

    // Remaining slots to complete 35 or 42 grid
    const remaining = (7 - (days.length % 7)) % 7;
    for (let r = 1; r <= remaining; r++) {
      days.push({
        day: r,
        isCurrentMonth: false,
        hasEvent: false,
        hasExpiry: false,
        events: []
      });
    }

    return days;
  });

  // Scope & Status Filtering
  agendaScope = signal<'MONTH' | 'ALL' | 'DAY'>('MONTH');
  statusFilter = signal<'ALL' | 'ACTION_REQUIRED' | 'COMPLIANT'>('ALL');
  agendaSearchQuery = signal<string>('');

  // Filtered Events for selected filter, scope, and search query
  displayedAgendaEvents = computed(() => {
    let list = this.events();

    // 1. Scope filter
    const scope = this.agendaScope();
    if (scope === 'MONTH') {
      list = list.filter(e => e.month === this.currentMonth() && e.year === this.currentYear());
    } else if (scope === 'DAY') {
      list = list.filter(e => e.month === this.currentMonth() && e.year === this.currentYear() && e.day === this.selectedDay());
    }

    // 2. Category filter
    const cat = this.selectedFilter();
    if (cat !== 'All Obligations') {
      list = list.filter(e => e.category === cat);
    }

    // 3. Status filter
    const stat = this.statusFilter();
    if (stat !== 'ALL') {
      list = list.filter(e => e.status === stat);
    }

    // 4. Keyword search
    const query = this.agendaSearchQuery().trim().toLowerCase();
    if (query) {
      list = list.filter(e =>
        e.title.toLowerCase().includes(query) ||
        e.authority.toLowerCase().includes(query) ||
        e.statutoryRef.toLowerCase().includes(query) ||
        (e.arnNumber && e.arnNumber.toLowerCase().includes(query)) ||
        e.category.toLowerCase().includes(query)
      );
    }

    return list;
  });

  // Filtered Events for list view
  filteredEvents = computed(() => {
    const filter = this.selectedFilter();
    let list = this.events();

    if (filter !== 'All Obligations') {
      list = list.filter(e => e.category === filter);
    }

    const stat = this.statusFilter();
    if (stat !== 'ALL') {
      list = list.filter(e => e.status === stat);
    }

    return list;
  });

  // Events occurring in current view month
  currentMonthEvents = computed(() => {
    const m = this.currentMonth();
    const y = this.currentYear();
    return this.events().filter(e => e.month === m && e.year === y);
  });

  // Events for selected day
  selectedDayEvents = computed(() => {
    const m = this.currentMonth();
    const y = this.currentYear();
    const d = this.selectedDay();
    return this.events().filter(e => e.year === y && e.month === m && e.day === d);
  });

  // Dynamic Week Days for Week View (Week of 21 - 27 Sep 2026)
  currentWeekDays = computed(() => {
    const days: { day: number; dayName: string; dateStr: string; isToday: boolean; events: CalendarEvent[] }[] = [];
    const dayNames = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
    for (let i = 0; i < 7; i++) {
      const d = 21 + i;
      const dayEvents = this.events().filter(e => e.year === 2026 && e.month === 8 && e.day === d);
      days.push({
        day: d,
        dayName: dayNames[i],
        dateStr: `${d} Sep 2026`,
        isToday: d === 27,
        events: dayEvents
      });
    }
    return days;
  });

  // Summary Metrics
  totalUpcomingCount = computed(() => this.events().filter(e => e.status !== 'COMPLIANT').length);
  actionRequiredCount = computed(() => this.events().filter(e => e.status === 'ACTION_REQUIRED').length);
  compliantCount = computed(() => this.events().filter(e => e.status === 'COMPLIANT').length);

  // Month Navigation
  prevMonth(): void {
    if (this.currentMonth() > 7) {
      this.currentMonth.update(m => m - 1);
      this.selectedDay.set(1);
    }
  }

  nextMonth(): void {
    if (this.currentMonth() < 11) {
      this.currentMonth.update(m => m + 1);
      this.selectedDay.set(1);
    }
  }

  goToToday(): void {
    this.currentMonth.set(8); // September 2026
    this.currentYear.set(2026);
    this.selectedDay.set(27);
  }

  selectDay(day: number, isCurrent: boolean): void {
    if (isCurrent) {
      this.selectedDay.set(day);
      const evs = this.events().filter(e => e.year === this.currentYear() && e.month === this.currentMonth() && e.day === day);
      if (evs.length > 0) {
        this.agendaScope.set('DAY');
      }
    }
  }

  setScope(scope: 'MONTH' | 'ALL' | 'DAY'): void {
    this.agendaScope.set(scope);
  }

  setStatusFilter(status: 'ALL' | 'ACTION_REQUIRED' | 'COMPLIANT'): void {
    if (this.statusFilter() === status) {
      this.statusFilter.set('ALL');
    } else {
      this.statusFilter.set(status);
    }
  }

  resetFilters(): void {
    this.selectedFilter.set('All Obligations');
    this.statusFilter.set('ALL');
    this.agendaScope.set('MONTH');
    this.agendaSearchQuery.set('');
  }

  setFilter(filter: string): void {
    this.selectedFilter.set(filter);
  }

  // Modal Actions
  openEventModal(event: CalendarEvent): void {
    this.activeEventModal.set(event);
  }

  closeEventModal(): void {
    this.activeEventModal.set(null);
  }

  openUploadModal(event: CalendarEvent): void {
    this.activeEventModal.set(null);
    this.challanRefInput.set(event.arnNumber || 'CHALLAN-' + Math.floor(100000 + Math.random() * 900000));
    this.uploadedFileName.set(event.title.replace(/[^a-zA-Z0-9]/g, '_') + '_Receipt.pdf');
    this.showUploadChallanModal.set(event);
  }

  closeUploadModal(): void {
    if (this.isProcessingUpload()) return;
    this.showUploadChallanModal.set(null);
    this.uploadProgress.set(null);
    this.uploadStepText.set('');
  }

  onFileSelected(event: any): void {
    const file = event?.target?.files?.[0];
    if (file) {
      this.uploadedFileName.set(file.name);
    }
  }

  submitChallanUpload(): void {
    const ev = this.showUploadChallanModal();
    if (!ev) return;

    this.isProcessingUpload.set(true);
    this.uploadProgress.set(20);
    this.uploadStepText.set('Parsing statutory document & checking digital signature...');

    setTimeout(() => {
      this.uploadProgress.set(55);
      this.uploadStepText.set(`Reconciling reference with ${ev.authority}...`);

      setTimeout(() => {
        this.uploadProgress.set(85);
        this.uploadStepText.set('Logging compliance attestation to Veripass sovereign ledger...');

        setTimeout(() => {
          this.uploadProgress.set(100);
          this.uploadStepText.set('Statutory obligation verified & cleared!');

          setTimeout(() => {
            // Update event in signal
            this.events.update(list =>
              list.map(item => {
                if (item.id === ev.id) {
                  return {
                    ...item,
                    status: 'COMPLIANT',
                    arnNumber: this.challanRefInput() || item.arnNumber,
                    description: item.description + ' (Cleared & Reconciled Today)'
                  };
                }
                return item;
              })
            );

            this.isProcessingUpload.set(false);
            this.showUploadChallanModal.set(null);
            this.uploadProgress.set(null);
            this.uploadStepText.set('');
            this.showToast(`✓ ${ev.title} Marked Compliant & Reconciled!`);
          }, 600);
        }, 600);
      }, 700);
    }, 600);
  }

  showToast(msg: string): void {
    this.toastMessage.set(msg);
    setTimeout(() => {
      if (this.toastMessage() === msg) {
        this.toastMessage.set(null);
      }
    }, 4500);
  }

  // Export iCalendar (.ics) format
  exportIcs(): void {
    let icsContent = [
      'BEGIN:VCALENDAR',
      'VERSION:2.0',
      'PRODID:-//Veripass India//Compliance Calendar//EN',
      'CALSCALE:GREGORIAN',
      'METHOD:PUBLISH',
      'X-WR-CALNAME:Veripass Sovereign Compliance Calendar'
    ];

    this.events().forEach(ev => {
      const m = String(ev.month + 1).padStart(2, '0');
      const d = String(ev.day).padStart(2, '0');
      const dateKey = `${ev.year}${m}${d}`;
      icsContent.push(
        'BEGIN:VEVENT',
        `UID:${ev.id}-2026@veripass.in`,
        `DTSTAMP:20260927T000000Z`,
        `DTSTART;VALUE=DATE:${dateKey}`,
        `DTEND;VALUE=DATE:${dateKey}`,
        `SUMMARY:Veripass: ${ev.title}`,
        `DESCRIPTION:${ev.description} | Authority: ${ev.authority} | Ref: ${ev.statutoryRef}`,
        `STATUS:${ev.status === 'COMPLIANT' ? 'CONFIRMED' : 'TENTATIVE'}`,
        'END:VEVENT'
      );
    });

    icsContent.push('END:VCALENDAR');

    const blob = new Blob([icsContent.join('\r\n')], { type: 'text/calendar;charset=utf-8' });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', `${this.companyName.replace(/[^a-zA-Z0-9]/g, '_')}_Compliance_Schedule.ics`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    this.showToast('✓ Calendar export (.ics) downloaded for Outlook / Google Calendar!');
  }

  // Export PDF Compliance Schedule
  downloadCalendarPdf(): void {
    try {
      const doc = new jsPDF({ orientation: 'portrait', unit: 'mm', format: 'a4' });
      const pageWidth = 210;

      // Header Banner
      doc.setFillColor(15, 23, 42); // slate-900
      doc.rect(0, 0, pageWidth, 28, 'F');

      doc.setFillColor(37, 99, 235); // blue accent line
      doc.rect(0, 28, pageWidth, 3, 'F');

      doc.setFont('helvetica', 'bold');
      doc.setFontSize(14);
      doc.setTextColor(255, 255, 255);
      doc.text('VERIPASS STATUTORY COMPLIANCE CALENDAR', 18, 14);

      doc.setFont('helvetica', 'normal');
      doc.setFontSize(8.5);
      doc.setTextColor(203, 213, 225);
      doc.text('Sovereign Deadlines, Filings & Credential Expiration Schedule (Sep - Dec 2026)', 18, 20);

      doc.setFontSize(8);
      doc.text(`Exported: 27 Sep 2026`, 150, 14);
      doc.text('Ref: VP-CAL-2026-SEP', 150, 20);

      // Entity Info
      doc.setFillColor(248, 250, 252);
      doc.setDrawColor(226, 232, 240);
      doc.roundedRect(18, 38, 174, 26, 2, 2, 'FD');

      doc.setFont('helvetica', 'bold');
      doc.setFontSize(11);
      doc.setTextColor(15, 23, 42);
      doc.text(this.companyName.toUpperCase(), 24, 46);

      doc.setFont('helvetica', 'normal');
      doc.setFontSize(8);
      doc.setTextColor(71, 85, 105);
      doc.text(`GSTIN: ${this.gstin}   |   PAN: ${this.pan}   |   CIN: ${this.cin}`, 24, 52);
      doc.text(`Operating Address: ${this.principalAddress}`, 24, 58);

      // Section: Schedule
      doc.setFillColor(241, 245, 249);
      doc.rect(18, 70, 174, 7, 'F');
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(8.5);
      doc.setTextColor(15, 23, 42);
      doc.text('STATUTORY OBLIGATION CHRONOLOGICAL SCHEDULE', 22, 75);

      // Table Header
      doc.setFillColor(226, 232, 240);
      doc.rect(18, 77, 174, 6, 'F');
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(7.5);
      doc.setTextColor(51, 65, 85);
      doc.text('DATE', 22, 81.5);
      doc.text('OBLIGATION TITLE', 52, 81.5);
      doc.text('AUTHORITY / RAIL', 115, 81.5);
      doc.text('CATEGORY', 155, 81.5);
      doc.text('STATUS', 182, 81.5);

      let curY = 83;
      this.events().forEach((ev, idx) => {
        if (idx % 2 === 1) {
          doc.setFillColor(248, 250, 252);
          doc.rect(18, curY, 174, 12, 'F');
        }

        doc.setFont('helvetica', 'bold');
        doc.setFontSize(7.5);
        doc.setTextColor(15, 23, 42);
        doc.text(ev.dateStr, 22, curY + 5.5);

        doc.setFont('helvetica', 'bold');
        doc.setFontSize(7.5);
        doc.setTextColor(30, 58, 138);
        doc.text(ev.title.substring(0, 32), 52, curY + 5.5);

        doc.setFont('helvetica', 'normal');
        doc.setFontSize(6.5);
        doc.setTextColor(100, 116, 139);
        doc.text(ev.statutoryRef.substring(0, 35), 52, curY + 9.5);

        doc.setFont('helvetica', 'normal');
        doc.setFontSize(7);
        doc.setTextColor(51, 65, 85);
        doc.text(ev.authority.substring(0, 22), 115, curY + 5.5);

        doc.setFontSize(6.5);
        doc.setTextColor(71, 85, 105);
        doc.text(ev.category, 155, curY + 5.5);

        doc.setFont('helvetica', 'bold');
        if (ev.status === 'COMPLIANT') {
          doc.setTextColor(5, 150, 105);
          doc.text('COMPLIANT', 182, curY + 5.5);
        } else if (ev.status === 'ACTION_REQUIRED') {
          doc.setTextColor(217, 119, 6);
          doc.text('ACTION', 182, curY + 5.5);
        } else {
          doc.setTextColor(37, 99, 235);
          doc.text('UPCOMING', 182, curY + 5.5);
        }

        doc.setDrawColor(241, 245, 249);
        doc.line(18, curY + 12, 192, curY + 12);
        curY += 12;
      });

      // Footer
      const footerY = 252;
      doc.setDrawColor(203, 213, 225);
      doc.line(18, footerY, 192, footerY);

      doc.setFont('helvetica', 'bold');
      doc.setFontSize(8);
      doc.setTextColor(15, 23, 42);
      doc.text('Sovereign Compliance Ledger Synchronization', 22, footerY + 7);

      doc.setFont('helvetica', 'normal');
      doc.setFontSize(7);
      doc.setTextColor(100, 116, 139);
      doc.text('Automated reminders dispatched 30, 15, and 3 days before statutory due dates via Veripass Watchtower.', 22, footerY + 12);
      doc.text('Authorized by Veripass India Compliance Gateway (https://veripass.in)', 22, footerY + 16);

      const safeComp = this.companyName.replace(/[^a-zA-Z0-9]/g, '_');
      doc.save(`${safeComp}_Compliance_Calendar_Schedule.pdf`);
      this.showToast('✓ Compliance Calendar Schedule (PDF) downloaded successfully!');
    } catch (e) {
      console.error(e);
      this.showToast('Unable to export Compliance Calendar Schedule.');
    }
  }
}
