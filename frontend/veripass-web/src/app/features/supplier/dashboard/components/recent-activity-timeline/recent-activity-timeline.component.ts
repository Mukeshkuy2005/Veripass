import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

export interface RecentActivityItem {
  id: string;
  date: string;
  fullDate?: string;
  title: string;
  subtitle: string;
  status: 'VERIFIED' | 'IN_REVIEW' | 'PENDING' | 'ACTION';
  tag?: string;
}

@Component({
  selector: 'app-recent-activity-timeline',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './recent-activity-timeline.component.html',
  styleUrl: './recent-activity-timeline.component.css'
})
export class RecentActivityTimelineComponent {
  @Input() activities: RecentActivityItem[] = [];
}
