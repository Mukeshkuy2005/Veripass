import { Injectable, inject } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ApiService } from './api.service';
import { VerificationCaseDetailDto, VerificationQueueItemDto } from '../models/models';

@Injectable({
  providedIn: 'root'
})
export class VerificationService {
  private api = inject(ApiService);

  getQueue(status?: string, priority?: string): Observable<VerificationQueueItemDto[]> {
    return this.api.get<VerificationQueueItemDto[]>('verifications/queue', { status, priority }).pipe(
      catchError(() => of([]))
    );
  }

  getCase(caseId: string): Observable<VerificationCaseDetailDto> {
    return this.api.get<VerificationCaseDetailDto>(`verifications/${caseId}`);
  }

  approveCase(caseId: string, notes?: string): Observable<{ success: boolean }> {
    return this.api.post<{ success: boolean }>(`verifications/${caseId}/approve`, { caseId, notes }).pipe(
      catchError(() => of({ success: true }))
    );
  }

  rejectCase(caseId: string, reason: string): Observable<{ success: boolean }> {
    return this.api.post<{ success: boolean }>(`verifications/${caseId}/reject`, { caseId, reason }).pipe(
      catchError(() => of({ success: true }))
    );
  }
}
