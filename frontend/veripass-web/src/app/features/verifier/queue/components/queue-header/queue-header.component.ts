import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { VerificationQueueSummary } from '../../models/verification-queue.model';

@Component({
  selector: 'app-queue-header',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './queue-header.component.html',
  styleUrl: './queue-header.component.css'
})
export class QueueHeaderComponent {
  @Input() scope: 'my' | 'all' = 'all';
  @Input() totalCount: number = 0;
  @Input() summary: VerificationQueueSummary | null = null;
  @Input() loading: boolean = false;

  @Output() scopeChange = new EventEmitter<'my' | 'all'>();
  @Output() refresh = new EventEmitter<void>();

  selectScope(newScope: 'my' | 'all'): void {
    if (this.scope !== newScope) {
      this.scopeChange.emit(newScope);
    }
  }

  onRefresh(): void {
    this.refresh.emit();
  }
}
