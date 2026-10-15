import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

export interface ActionRequiredItem {
  id: string;
  title: string;
  subtitle: string;
  urgency: 'critical' | 'warning' | 'info';
  actionLabel: string;
  routerLink: string;
  queryParams?: Record<string, any>;
}

@Component({
  selector: 'app-action-required-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './action-required-list.component.html',
  styleUrl: './action-required-list.component.css'
})
export class ActionRequiredListComponent {
  @Input() items: ActionRequiredItem[] = [
    {
      id: 'act-1',
      title: 'ISO 9001 certificate expires in 27 days',
      subtitle: 'Upload the renewed quality management certificate before deadline',
      urgency: 'warning',
      actionLabel: 'Resolve →',
      routerLink: '/supplier/documents'
    },
    {
      id: 'act-2',
      title: 'Business address verification incomplete',
      subtitle: 'Additional utility evidence is required to confirm registered address',
      urgency: 'warning',
      actionLabel: 'Complete →',
      routerLink: '/supplier/verification'
    },
    {
      id: 'act-3',
      title: 'Buyer requested statutory verification',
      subtitle: 'XYZ Corporation requested access to verified bank & GST credentials',
      urgency: 'info',
      actionLabel: 'Review →',
      routerLink: '/supplier/requests'
    }
  ];
}
