import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-queue-pagination',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './queue-pagination.component.html',
  styleUrl: './queue-pagination.component.css'
})
export class QueuePaginationComponent {
  @Input() page: number = 1;
  @Input() pageSize: number = 25;
  @Input() totalCount: number = 0;
  @Input() totalPages: number = 0;
  @Input() loading: boolean = false;

  @Output() pageChange = new EventEmitter<number>();
  @Output() pageSizeChange = new EventEmitter<number>();

  get startItemIndex(): number {
    if (this.totalCount === 0) return 0;
    return (this.page - 1) * this.pageSize + 1;
  }

  get endItemIndex(): number {
    if (this.totalCount === 0) return 0;
    return Math.min(this.page * this.pageSize, this.totalCount);
  }

  get visiblePages(): number[] {
    const pages: number[] = [];
    const maxVisible = 5;
    let start = Math.max(1, this.page - Math.floor(maxVisible / 2));
    let end = Math.min(this.totalPages, start + maxVisible - 1);

    if (end - start + 1 < maxVisible) {
      start = Math.max(1, end - maxVisible + 1);
    }

    for (let i = start; i <= end; i++) {
      pages.push(i);
    }
    return pages;
  }

  onPageSelect(newPage: number): void {
    if (newPage >= 1 && newPage <= this.totalPages && newPage !== this.page) {
      this.pageChange.emit(newPage);
    }
  }

  onPageSizeSelect(event: Event): void {
    const select = event.target as HTMLSelectElement;
    const newSize = parseInt(select.value, 10);
    if (!isNaN(newSize) && newSize !== this.pageSize) {
      this.pageSizeChange.emit(newSize);
    }
  }
}
