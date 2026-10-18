import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { BuyerTrustScoreDto, BuyerVerificationProfileDto } from '../models/models';

@Injectable({
  providedIn: 'root'
})
export class BuyerTrustScoreService {
  private http = inject(HttpClient);
  private readonly baseUrl = 'http://localhost:5000/api/v1/buyers';

  getSupplierTrustScore(buyerId: string, supplierId: string): Observable<BuyerTrustScoreDto> {
    return this.http.get<BuyerTrustScoreDto>(`${this.baseUrl}/${buyerId}/suppliers/${supplierId}/trust-score`);
  }

  getBatchTrustScores(buyerId: string, supplierIds: string[]): Observable<BuyerTrustScoreDto[]> {
    return this.http.post<BuyerTrustScoreDto[]>(`${this.baseUrl}/${buyerId}/trust-scores/batch`, { supplierIds });
  }

  getScoringProfile(buyerId: string): Observable<BuyerVerificationProfileDto> {
    return this.http.get<BuyerVerificationProfileDto>(`${this.baseUrl}/${buyerId}/scoring-profile`);
  }

  updateScoringProfile(buyerId: string, payload: { name: string; description?: string; requirements: any[] }): Observable<BuyerVerificationProfileDto> {
    return this.http.put<BuyerVerificationProfileDto>(`${this.baseUrl}/${buyerId}/scoring-profile`, payload);
  }

  getTrustScoreHistory(buyerId: string, supplierId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/${buyerId}/suppliers/${supplierId}/trust-score/history`);
  }
}
