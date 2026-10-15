import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-vp-footer',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './vp-footer.component.html',
  styleUrl: './vp-footer.component.css'
})
export class VpFooterComponent {}
