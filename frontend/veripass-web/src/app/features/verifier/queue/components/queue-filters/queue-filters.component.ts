import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { VerificationQueueFilterParams } from '../../models/verification-queue.model';

@Component({
  selector: 'app-queue-filters',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './queue-filters.component.html',
  styleUrl: './queue-filters.component.css'
})
export class QueueFiltersComponent implements OnChanges {
  @Input() initialFilters: VerificationQueueFilterParams | null = null;
  @Input() loading: boolean = false;

  @Output() apply = new EventEmitter<VerificationQueueFilterParams>();
  @Output() reset = new EventEmitter<void>();

  // Filter form model
  searchQuery: string = '';
  filterStatus: string = 'PENDING';
  filterPriority: string = 'ALL';
  filterType: string = 'ALL';
  filterRisk: string = 'ALL';
  filterDate: string = 'ALL';

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['initialFilters'] && this.initialFilters) {
      this.searchQuery = this.initialFilters.search || '';
      this.filterStatus = this.initialFilters.status || 'PENDING';
      this.filterPriority = this.initialFilters.priority || 'ALL';
      this.filterType = this.initialFilters.type || 'ALL';
      this.filterRisk = this.initialFilters.risk || 'ALL';
      this.filterDate = this.initialFilters.dateRange || 'ALL';
    }
  }

  onApply(): void {
    const filters: VerificationQueueFilterParams = {
      scope: this.initialFilters?.scope || 'all',
      search: this.searchQuery.trim() || undefined,
      status: this.filterStatus,
      priority: this.filterPriority,
      type: this.filterType,
      risk: this.filterRisk,
      dateRange: this.filterDate,
      page: 1, // Reset to page 1 on filter application
      pageSize: this.initialFilters?.pageSize || 25,
      sortBy: this.initialFilters?.sortBy,
      sortDirection: this.initialFilters?.sortDirection
    };

    this.apply.emit(filters);
  }

  onReset(): void {
    this.searchQuery = '';
    this.filterStatus = 'PENDING';
    this.filterPriority = 'ALL';
    this.filterType = 'ALL';
    this.filterRisk = 'ALL';
    this.filterDate = 'ALL';

    this.reset.emit();
  }

  onSearchKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      this.onApply();
    }
  }
}
