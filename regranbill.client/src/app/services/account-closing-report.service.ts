import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AccountClosingReport } from '../models/account-closing-report.model';

@Injectable({
  providedIn: 'root'
})
export class AccountClosingReportService {
  private readonly url = '/api/account-closing-report';

  constructor(private http: HttpClient) {}

  getReport(from?: string, to?: string, accountId?: number, historyAccountId?: number): Observable<AccountClosingReport> {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    if (accountId) params = params.set('accountId', accountId.toString());
    if (historyAccountId) params = params.set('historyAccountId', historyAccountId.toString());
    return this.http.get<AccountClosingReport>(this.url, { params });
  }
}
