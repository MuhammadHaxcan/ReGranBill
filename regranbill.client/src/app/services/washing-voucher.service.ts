import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import {
  CreateWashingVoucherRequest,
  LatestUnwashedRateDto,
  WashingVoucherDto,
  WashingVoucherListDto
} from '../models/washing-voucher.model';

@Injectable({ providedIn: 'root' })
export class WashingVoucherService {
  private url = '/api/washing-vouchers';

  constructor(private http: HttpClient) {}

  getAll(): Observable<WashingVoucherListDto[]> {
    return this.http.get<WashingVoucherListDto[]>(this.url);
  }

  getById(id: number): Observable<WashingVoucherDto> {
    return this.http.get<WashingVoucherDto>(`${this.url}/${id}`);
  }

  getNextNumber(): Observable<string> {
    return this.http.get<{ voucherNumber: string }>(`${this.url}/next-number`).pipe(
      map(res => res.voucherNumber)
    );
  }

  getLatestUnwashedRate(vendorId: number, accountId: number): Observable<LatestUnwashedRateDto | null> {
    return this.http.get<LatestUnwashedRateDto | { rate: null }>(
      `${this.url}/latest-unwashed-rate`,
      { params: { vendorId, accountId } as any }
    ).pipe(
      map(res => 'accountId' in res ? res as LatestUnwashedRateDto : null)
    );
  }

  create(request: CreateWashingVoucherRequest): Observable<WashingVoucherDto> {
    return this.http.post<WashingVoucherDto>(this.url, request);
  }

  openPrintInNewTab(id: number, voucherNumber?: string | null): void {
    const key = voucherNumber?.trim() || id.toString();
    window.open(`/print-wsh/${encodeURIComponent(key)}`, '_blank');
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}
