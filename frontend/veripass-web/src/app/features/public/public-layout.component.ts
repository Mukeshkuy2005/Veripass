import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { VpHeaderComponent } from '../../shared/components/vp-header/vp-header.component';
import { VpFooterComponent } from '../../shared/components/vp-footer/vp-footer.component';

@Component({
  selector: 'app-public-layout',
  standalone: true,
  imports: [CommonModule, RouterModule, VpHeaderComponent, VpFooterComponent],
  templateUrl: './public-layout.component.html',
  styleUrl: './public-layout.component.css'
})
export class PublicLayoutComponent {}
