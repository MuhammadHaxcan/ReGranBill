import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CustomerLedger } from '../models/customer-ledger.model';

@Injectable({
  providedIn: 'root',
})
export class CustomerLedgerService {
  private url = '/api/customer-ledger';

  constructor(private http: HttpClient) {}

  getLedger(accountId: number, fromDate?: string, toDate?: string): Observable<CustomerLedger> {
    let params = new HttpParams();
    if (fromDate) {
      params = params.set('fromDate', fromDate);
    }
    if (toDate) {
      params = params.set('toDate', toDate);
    }
    return this.http.get<CustomerLedger>(`${this.url}/${accountId}`, { params });
  }

  getAllLedgers(partyType: string, fromDate?: string, toDate?: string): Observable<CustomerLedger[]> {
    let params = new HttpParams().set('partyType', partyType);
    if (fromDate) {
      params = params.set('fromDate', fromDate);
    }
    if (toDate) {
      params = params.set('toDate', toDate);
    }
    return this.http.get<CustomerLedger[]>(`${this.url}/all`, { params });
  }
}
