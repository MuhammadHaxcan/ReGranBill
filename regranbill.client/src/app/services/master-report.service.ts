import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { MasterReport } from '../models/master-report.model';

@Injectable({
  providedIn: 'root',
})
export class MasterReportService {
  private url = '/api/master-report';

  constructor(private http: HttpClient) {}

  getReport(from?: string, to?: string, categoryId?: number, accountId?: number): Observable<MasterReport> {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    if (categoryId) params = params.set('categoryId', categoryId.toString());
    if (accountId) params = params.set('accountId', accountId.toString());
    return this.http.get<MasterReport>(this.url, { params });
  }
}
