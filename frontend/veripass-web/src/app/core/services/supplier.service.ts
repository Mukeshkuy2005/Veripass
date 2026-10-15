import { Injectable, inject } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ApiService } from './api.service';
import { SupplierCompareDto, SupplierSearchResultDto } from '../models/models';

@Injectable({
  providedIn: 'root'
})
export class SupplierService {
  private api = inject(ApiService);

  searchSuppliers(filters: Record<string, any>): Observable<SupplierSearchResultDto[]> {
    return this.api.get<SupplierSearchResultDto[]>('suppliers/search', filters).pipe(
      catchError(() => of([]))
    );
  }

  compareSuppliers(businessIds: string[]): Observable<SupplierCompareDto[]> {
    return this.api.post<SupplierCompareDto[]>('suppliers/compare', businessIds).pipe(
      catchError(() => of([]))
    );
  }

  inviteSupplier(email: string, phone?: string): Observable<{ invitationId: string }> {
    return this.api.post<{ invitationId: string }>('relationships/invite', { email, phone }).pipe(
      catchError(() => of({ invitationId: '' }))
    );
  }
}
