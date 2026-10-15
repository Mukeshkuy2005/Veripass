import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-contact',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './contact.component.html',
  styleUrl: './contact.component.css'
})
export class ContactComponent {
  submitted = signal(false);

  formData = {
    name: '',
    email: '',
    reason: 'General Support',
    business: '',
    message: ''
  };

  submitMessage(): void {
    if (this.formData.name && this.formData.email && this.formData.message) {
      this.submitted.set(true);
    }
  }

  resetForm(): void {
    this.submitted.set(false);
    this.formData = {
      name: '',
      email: '',
      reason: 'General Support',
      business: '',
      message: ''
    };
  }
}
