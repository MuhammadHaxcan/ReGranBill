import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface DownstreamUsage {
  voucherId: number;
  voucherNumber: string;
  voucherType: string;
  date: string;
  lotId: number;
  lotNumber: string;
  transactionType: string;
  qtyDelta: number | null;
  weightKgDelta: number;
}

@Injectable({ providedIn: 'root' })
export class DownstreamUsageService {
  constructor(private http: HttpClient) {}

  forPurchase(id: number): Observable<DownstreamUsage[]> {
    return this.http.get<DownstreamUsage[]>(`/api/purchase-vouchers/${id}/downstream`);
  }

  forPurchaseReturn(id: number): Observable<DownstreamUsage[]> {
    return this.http.get<DownstreamUsage[]>(`/api/purchase-returns/${id}/downstream`);
  }

  forWashing(id: number): Observable<DownstreamUsage[]> {
    return this.http.get<DownstreamUsage[]>(`/api/washing-vouchers/${id}/downstream`);
  }

  forProduction(id: number): Observable<DownstreamUsage[]> {
    return this.http.get<DownstreamUsage[]>(`/api/production-vouchers/${id}/downstream`);
  }
}
