import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { VerificationQueueItem } from '../../models/verification-queue.model';
import { VerificationQueueService } from '../../services/verification-queue.service';

@Component({
  selector: 'app-queue-table',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './queue-table.component.html',
  styleUrl: './queue-table.component.css'
})
export class QueueTableComponent {
  @Input() items: VerificationQueueItem[] = [];
  @Input() sortBy: string = 'submitted';
  @Input() sortDirection: 'asc' | 'desc' = 'desc';

  @Output() sort = new EventEmitter<{ sortBy: string; sortDirection: 'asc' | 'desc' }>();

  constructor(public queueService: VerificationQueueService) {}

  onSort(field: string): void {
    if (this.sortBy === field) {
      const nextDir: 'asc' | 'desc' = this.sortDirection === 'asc' ? 'desc' : 'asc';
      this.sort.emit({ sortBy: field, sortDirection: nextDir });
    } else {
      this.sort.emit({ sortBy: field, sortDirection: 'desc' });
    }
  }

  getSortIcon(field: string): string {
    if (this.sortBy !== field) return '↕';
    return this.sortDirection === 'asc' ? '↑' : '↓';
  }
}
