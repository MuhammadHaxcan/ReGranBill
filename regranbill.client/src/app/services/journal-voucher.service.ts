import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { CreateJournalVoucherRequest, JournalVoucher } from '../models/journal-voucher.model';

@Injectable({
  providedIn: 'root',
})
export class JournalVoucherService {
  private url = '/api/journal-vouchers';

  constructor(private http: HttpClient) {}

  getAll(): Observable<JournalVoucher[]> {
    return this.http.get<JournalVoucher[]>(this.url);
  }

  getById(id: number): Observable<JournalVoucher> {
    return this.http.get<JournalVoucher>(`${this.url}/${id}`);
  }

  getNextNumber(): Observable<string> {
    return this.http.get<{ voucherNumber: string }>(`${this.url}/next-number`).pipe(
      map(res => res.voucherNumber)
    );
  }

  create(request: CreateJournalVoucherRequest): Observable<JournalVoucher> {
    return this.http.post<JournalVoucher>(this.url, request);
  }

  update(id: number, request: CreateJournalVoucherRequest): Observable<JournalVoucher> {
    return this.http.put<JournalVoucher>(`${this.url}/${id}`, request);
  }
}
