import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AvailableInventoryLot } from '../models/inventory-lot.model';

@Injectable({ providedIn: 'root' })
export class InventoryLotService {
  private readonly url = '/api/inventory-lots';

  constructor(private http: HttpClient) {}

  getAvailableForWashing(vendorId: number, accountId: number, voucherId?: number | null): Observable<AvailableInventoryLot[]> {
    return this.http.get<AvailableInventoryLot[]>(`${this.url}/available-for-washing`, {
      params: {
        vendorId,
        accountId,
        ...(voucherId ? { voucherId } : {})
      } as any
    });
  }

  getAvailableForProduction(accountId: number, voucherId?: number | null): Observable<AvailableInventoryLot[]> {
    return this.http.get<AvailableInventoryLot[]>(`${this.url}/available-for-production`, {
      params: {
        accountId,
        ...(voucherId ? { voucherId } : {})
      } as any
    });
  }

  getAvailableForPurchaseReturn(vendorId: number, accountId: number, voucherId?: number | null): Observable<AvailableInventoryLot[]> {
    return this.http.get<AvailableInventoryLot[]>(`${this.url}/available-for-purchase-return`, {
      params: {
        vendorId,
        accountId,
        ...(voucherId ? { voucherId } : {})
      } as any
    });
  }
}
