import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class DeliveryChallanService {
  private url = '/api/delivery-challans';

  constructor(private http: HttpClient) {}

  getAll(): Observable<any[]> {
    return this.http.get<any[]>(this.url);
  }

  getById(id: number): Observable<any> {
    return this.http.get<any>(`${this.url}/${id}`);
  }

  getNextNumber(): Observable<string> {
    return this.http.get<{ dcNumber: string }>(`${this.url}/next-number`).pipe(
      map(res => res.dcNumber)
    );
  }

  create(request: any): Observable<any> {
    return this.http.post<any>(this.url, request);
  }

  update(id: number, request: any): Observable<any> {
    return this.http.put<any>(`${this.url}/${id}`, request);
  }

  updateRates(id: number, request: { lines: { entryId: number; rate: number }[] }): Observable<any> {
    return this.http.patch<any>(`${this.url}/${id}/rates`, request);
  }

  getPdf(id: number): Observable<Blob> {
    return this.http.get(`${this.url}/${id}/pdf`, { responseType: 'blob' });
  }

  openPdfInNewTab(id: number): void {
    this.getPdf(id).subscribe(blob => {
      const url = URL.createObjectURL(blob);
      window.open(url, '_blank');
    });
  }
}
