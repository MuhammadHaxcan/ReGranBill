import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { StatementOfAccount } from '../models/statement.model';

@Injectable({
  providedIn: 'root',
})
export class StatementService {
  private url = '/api/statements';

  constructor(private http: HttpClient) {}

  getStatement(accountId: number, fromDate?: string, toDate?: string): Observable<StatementOfAccount> {
    let params = new HttpParams();
    if (fromDate) params = params.set('fromDate', fromDate);
    if (toDate) params = params.set('toDate', toDate);
    return this.http.get<StatementOfAccount>(`${this.url}/${accountId}`, { params });
  }

  getStatementPdf(accountId: number, fromDate?: string, toDate?: string): Observable<Blob> {
    let params = new HttpParams();
    if (fromDate) params = params.set('fromDate', fromDate);
    if (toDate) params = params.set('toDate', toDate);
    return this.http.get(`${this.url}/${accountId}/pdf`, { params, responseType: 'blob' });
  }
}
