import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable, catchError, of, throwError } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private http = inject(HttpClient);
  public readonly baseUrl = 'http://localhost:5000/api/v1';

  get<T>(endpoint: string, params?: Record<string, any>): Observable<T> {
    const clean = endpoint.replace(/^\/+/, '');
    let httpParams = new HttpParams();
    if (params) {
      Object.keys(params).forEach(key => {
        if (params[key] !== null && params[key] !== undefined) {
          httpParams = httpParams.set(key, params[key]);
        }
      });
    }
    return this.http.get<T>(`${this.baseUrl}/${clean}`, { params: httpParams });
  }

  post<T>(endpoint: string, body: any): Observable<T> {
    const clean = endpoint.replace(/^\/+/, '');
    return this.http.post<T>(`${this.baseUrl}/${clean}`, body);
  }

  put<T>(endpoint: string, body: any): Observable<T> {
    const clean = endpoint.replace(/^\/+/, '');
    return this.http.put<T>(`${this.baseUrl}/${clean}`, body);
  }

  delete<T>(endpoint: string): Observable<T> {
    const clean = endpoint.replace(/^\/+/, '');
    return this.http.delete<T>(`${this.baseUrl}/${clean}`);
  }

  upload<T>(endpoint: string, formData: FormData): Observable<T> {
    const clean = endpoint.replace(/^\/+/, '');
    return this.http.post<T>(`${this.baseUrl}/${clean}`, formData);
  }

  getBlob(endpoint: string): Observable<Blob> {
    const clean = endpoint.replace(/^\/+/, '');
    return this.http.get(`${this.baseUrl}/${clean}`, { responseType: 'blob' });
  }
}
