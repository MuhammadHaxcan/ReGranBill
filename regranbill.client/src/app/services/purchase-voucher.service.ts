import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map, of } from 'rxjs';
import {
  PurchaseVoucherApiDto,
  PurchaseVoucherRateUpdateRequest,
  PurchaseVoucherUpsertRequest,
  PurchaseVoucherViewModel
} from '../models/purchase-voucher.model';
import { LatestProductRate } from '../models/latest-product-rate.model';

@Injectable({
  providedIn: 'root',
})
export class PurchaseVoucherService {
  private url = '/api/purchase-vouchers';

  constructor(private http: HttpClient) {}

  getAll(): Observable<PurchaseVoucherViewModel[]> {
    return this.http.get<PurchaseVoucherApiDto[]>(this.url).pipe(
      map(vouchers => vouchers.map(voucher => this.mapVoucher(voucher)))
    );
  }

  getById(id: number): Observable<PurchaseVoucherViewModel> {
    return this.http.get<PurchaseVoucherApiDto>(`${this.url}/${id}`).pipe(
      map(voucher => this.mapVoucher(voucher))
    );
  }

  getNextNumber(): Observable<string> {
    return this.http.get<{ voucherNumber: string }>(`${this.url}/next-number`).pipe(
      map(res => res.voucherNumber)
    );
  }

  getLatestRates(productIds: number[]): Observable<LatestProductRate[]> {
    const ids = productIds.filter(id => id > 0);
    if (ids.length === 0) return of([]);

    const query = encodeURIComponent(ids.join(','));
    return this.http.get<LatestProductRate[]>(`${this.url}/latest-rates?productIds=${query}`);
  }

  create(request: PurchaseVoucherUpsertRequest): Observable<PurchaseVoucherViewModel> {
    return this.http.post<PurchaseVoucherApiDto>(this.url, request).pipe(
      map(voucher => this.mapVoucher(voucher))
    );
  }

  update(id: number, request: PurchaseVoucherUpsertRequest): Observable<PurchaseVoucherViewModel> {
    return this.http.put<PurchaseVoucherApiDto>(`${this.url}/${id}`, request).pipe(
      map(voucher => this.mapVoucher(voucher))
    );
  }

  updateRates(id: number, request: PurchaseVoucherRateUpdateRequest): Observable<PurchaseVoucherViewModel> {
    return this.http.patch<PurchaseVoucherApiDto>(`${this.url}/${id}/rates`, request).pipe(
      map(voucher => this.mapVoucher(voucher))
    );
  }

  getPdf(id: number): Observable<Blob> {
    return this.http.get(`${this.url}/${id}/pdf`, { responseType: 'blob' });
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }

  openPdfInNewTab(id: number, voucherNumber?: string | null): void {
    const key = voucherNumber?.trim() || id.toString();
    window.open(`/print-pv/${encodeURIComponent(key)}`, '_blank');
  }

  private mapVoucher(voucher: PurchaseVoucherApiDto): PurchaseVoucherViewModel {
    return {
      ...voucher,
      vendorName: voucher.vendorName || '',
      lines: voucher.lines,
      cartage: voucher.cartage
        ? {
            ...voucher.cartage,
            transporterName: voucher.cartage.transporterName || ''
          }
        : null,
      journalVouchers: voucher.journalVouchers.map(summary => ({
        ...summary,
        entries: summary.entries.map(entry => ({
          ...entry,
          rbp: entry.rbp ?? null
        }))
      }))
    };
  }
}
