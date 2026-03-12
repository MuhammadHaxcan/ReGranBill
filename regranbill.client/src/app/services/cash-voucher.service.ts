import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { CashVoucher, CashVoucherMode, CreateCashVoucherRequest } from '../models/cash-voucher.model';

@Injectable({
  providedIn: 'root',
})
export class CashVoucherService {
  private url = '/api/cash-vouchers';

  constructor(private http: HttpClient) {}

  getNextNumber(mode: CashVoucherMode): Observable<string> {
    return this.http.get<{ voucherNumber: string }>(`${this.url}/${mode}/next-number`).pipe(
      map(res => res.voucherNumber)
    );
  }

  getById(mode: CashVoucherMode, id: number): Observable<CashVoucher> {
    return this.http.get<CashVoucher>(`${this.url}/${mode}/${id}`);
  }

  create(mode: CashVoucherMode, request: CreateCashVoucherRequest): Observable<CashVoucher> {
    return this.http.post<CashVoucher>(`${this.url}/${mode}`, request);
  }

  update(mode: CashVoucherMode, id: number, request: CreateCashVoucherRequest): Observable<CashVoucher> {
    return this.http.put<CashVoucher>(`${this.url}/${mode}/${id}`, request);
  }
}
