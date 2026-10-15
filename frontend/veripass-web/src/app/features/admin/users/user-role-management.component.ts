import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AdminStateService, AdminUser } from '../../../core/services/admin-state.service';

type UserTab = 'USERS' | 'ROLES' | 'PERMISSIONS';

@Component({
  selector: 'app-user-role-management',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './user-role-management.component.html',
  styleUrl: './user-role-management.component.css'
})
export class UserRoleManagementComponent {
  private adminState = inject(AdminStateService);

  users = this.adminState.users;
  activeTab: UserTab = 'USERS';
}
