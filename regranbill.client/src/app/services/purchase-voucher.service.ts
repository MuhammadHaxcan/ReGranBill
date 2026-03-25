import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import {
  PurchaseVoucherApiDto,
  PurchaseVoucherRateUpdateRequest,
  PurchaseVoucherUpsertRequest,
  PurchaseVoucherViewModel,
  PurchaseVoucherRbp
} from '../models/purchase-voucher.model';

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

  openPdfInNewTab(id: number): void {
    window.open(`/print-pv/${id}`, '_blank');
  }

  private mapVoucher(voucher: PurchaseVoucherApiDto): PurchaseVoucherViewModel {
    return {
      ...voucher,
      vendorName: voucher.vendorName || '',
      lines: voucher.lines.map(line => ({
        ...line,
        rbp: this.normalizeRbp(line.rbp)
      })),
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

  private normalizeRbp(value: string | null | undefined): PurchaseVoucherRbp {
    return value && value.trim().toLowerCase() === 'no' ? 'No' : 'Yes';
  }
}
