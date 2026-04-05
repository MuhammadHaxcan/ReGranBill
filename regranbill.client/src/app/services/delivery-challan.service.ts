import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map, of } from 'rxjs';
import {
  DeliveryChallanUpsertRequest,
  DeliveryChallanViewModel,
  UpdateDeliveryRatesRequest
} from '../models/delivery-challan.model';
import { LatestProductRate } from '../models/latest-product-rate.model';

@Injectable({
  providedIn: 'root',
})
export class DeliveryChallanService {
  private url = '/api/delivery-challans';

  constructor(private http: HttpClient) {}

  getAll(): Observable<DeliveryChallanViewModel[]> {
    return this.http.get<DeliveryChallanViewModel[]>(this.url);
  }

  getById(id: number): Observable<DeliveryChallanViewModel> {
    return this.http.get<DeliveryChallanViewModel>(`${this.url}/${id}`);
  }

  getNextNumber(): Observable<string> {
    return this.http.get<{ dcNumber: string }>(`${this.url}/next-number`).pipe(
      map(res => res.dcNumber)
    );
  }

  getLatestRates(productIds: number[]): Observable<LatestProductRate[]> {
    const ids = productIds.filter(id => id > 0);
    if (ids.length === 0) return of([]);

    const query = encodeURIComponent(ids.join(','));
    return this.http.get<LatestProductRate[]>(`${this.url}/latest-rates?productIds=${query}`);
  }

  create(request: DeliveryChallanUpsertRequest): Observable<DeliveryChallanViewModel> {
    return this.http.post<DeliveryChallanViewModel>(this.url, request);
  }

  update(id: number, request: DeliveryChallanUpsertRequest): Observable<DeliveryChallanViewModel> {
    return this.http.put<DeliveryChallanViewModel>(`${this.url}/${id}`, request);
  }

  updateRates(id: number, request: UpdateDeliveryRatesRequest): Observable<DeliveryChallanViewModel> {
    return this.http.patch<DeliveryChallanViewModel>(`${this.url}/${id}/rates`, request);
  }

  getPdf(id: number): Observable<Blob> {
    return this.http.get(`${this.url}/${id}/pdf`, { responseType: 'blob' });
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }

  openPdfInNewTab(id: number): void {
    window.open(`/print-dc/${id}`, '_blank');
  }
}
