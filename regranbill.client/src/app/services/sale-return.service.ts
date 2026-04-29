import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map, of } from 'rxjs';
import { SaleReturnViewModel, SaleReturnUpsertRequest, UpdateSaleReturnRatesRequest } from '../models/sale-return.model';
import { LatestProductRate } from '../models/latest-product-rate.model';

@Injectable({ providedIn: 'root' })
export class SaleReturnService {
  private readonly url = '/api/sale-returns';

  constructor(private http: HttpClient) {}

  getAll(): Observable<SaleReturnViewModel[]> {
    return this.http.get<SaleReturnViewModel[]>(this.url);
  }

  getById(id: number): Observable<SaleReturnViewModel> {
    return this.http.get<SaleReturnViewModel>(`${this.url}/${id}`);
  }

  getNextNumber(): Observable<string> {
    return this.http.get<{ srNumber: string }>(`${this.url}/next-number`).pipe(
      map(res => res.srNumber)
    );
  }

  getLatestRates(productIds: number[]): Observable<LatestProductRate[]> {
    const ids = productIds.filter(id => id > 0);
    if (ids.length === 0) return of([]);
    const query = encodeURIComponent(ids.join(','));
    return this.http.get<LatestProductRate[]>(`${this.url}/latest-rates?productIds=${query}`);
  }

  create(request: SaleReturnUpsertRequest): Observable<SaleReturnViewModel> {
    return this.http.post<SaleReturnViewModel>(this.url, request);
  }

  update(id: number, request: SaleReturnUpsertRequest): Observable<SaleReturnViewModel> {
    return this.http.put<SaleReturnViewModel>(`${this.url}/${id}`, request);
  }

  updateRates(id: number, request: UpdateSaleReturnRatesRequest): Observable<SaleReturnViewModel> {
    return this.http.patch<SaleReturnViewModel>(`${this.url}/${id}/rates`, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }

  getPdf(id: number): Observable<Blob> {
    return this.http.get(`${this.url}/${id}/pdf`, { responseType: 'blob' });
  }

  openPdfInNewTab(id: number): void {
    this.getPdf(id).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        window.open(url, '_blank');
      },
      error: () => { }
    });
  }
}