import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map, of } from 'rxjs';

export interface PurchaseReturnViewModel {
  id: number;
  prNumber: string;
  date: string;
  vendorId: number;
  vendorName?: string | null;
  vehicleNumber?: string | null;
  description?: string | null;
  voucherType: string;
  ratesAdded: boolean;
  lines: DeliveryChallanLineViewModel[];
  journalVouchers: JournalVoucherSummary[];
}

export interface DeliveryChallanLineViewModel {
  id: number;
  productId: number;
  productName?: string | null;
  packing?: string | null;
  packingWeightKg: number;
  rbp: string;
  qty: number;
  totalWeightKg?: number;
  rate: number;
  sortOrder: number;
}

export interface JournalVoucherSummary {
  id: number;
  voucherNumber: string;
  voucherType: string;
  ratesAdded: boolean;
  totalDebit: number;
  totalCredit: number;
}

export interface LatestProductRate {
  productId: number;
  rate: number;
  sourceVoucherNumber: string;
  sourceDate: string;
}

export interface PurchaseReturnUpsertRequest {
  date: Date;
  vendorId: number;
  vehicleNumber?: string | null;
  description?: string | null;
  lines: PurchaseReturnLineRequest[];
}

export interface PurchaseReturnLineRequest {
  productId: number;
  qty: number;
  totalWeightKg: number;
  rate: number;
  sortOrder: number;
}

export interface UpdatePurchaseReturnRatesRequest {
  lines: PurchaseReturnRateLineRequest[];
}

export interface PurchaseReturnRateLineRequest {
  entryId: number;
  rate: number;
}

@Injectable({ providedIn: 'root' })
export class PurchaseReturnService {
  private readonly url = '/api/purchase-returns';

  constructor(private http: HttpClient) {}

  getAll(): Observable<PurchaseReturnViewModel[]> {
    return this.http.get<PurchaseReturnViewModel[]>(this.url);
  }

  getById(id: number): Observable<PurchaseReturnViewModel> {
    return this.http.get<PurchaseReturnViewModel>(`${this.url}/${id}`);
  }

  getNextNumber(): Observable<string> {
    return this.http.get<{ prNumber: string }>(`${this.url}/next-number`).pipe(
      map(res => res.prNumber)
    );
  }

  getLatestRates(productIds: number[]): Observable<LatestProductRate[]> {
    const ids = productIds.filter(id => id > 0);
    if (ids.length === 0) return of([]);
    const query = encodeURIComponent(ids.join(','));
    return this.http.get<LatestProductRate[]>(`${this.url}/latest-rates?productIds=${query}`);
  }

  create(request: PurchaseReturnUpsertRequest): Observable<PurchaseReturnViewModel> {
    return this.http.post<PurchaseReturnViewModel>(this.url, request);
  }

  update(id: number, request: PurchaseReturnUpsertRequest): Observable<PurchaseReturnViewModel> {
    return this.http.put<PurchaseReturnViewModel>(`${this.url}/${id}`, request);
  }

  updateRates(id: number, request: UpdatePurchaseReturnRatesRequest): Observable<PurchaseReturnViewModel> {
    return this.http.patch<PurchaseReturnViewModel>(`${this.url}/${id}/rates`, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }

  getPdf(id: number): Observable<Blob> {
    return this.http.get(`${this.url}/${id}/pdf`, { responseType: 'blob' });
  }

  openPdfInNewTab(id: number, prNumber?: string | null): void {
    const key = prNumber?.trim() || id.toString();
    window.open(`/print-pr/${encodeURIComponent(key)}`, '_blank');
  }
}
