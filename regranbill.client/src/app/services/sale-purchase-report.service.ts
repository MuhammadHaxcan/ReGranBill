import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { SalePurchaseReport, SalePurchaseReportType } from '../models/sale-purchase-report.model';

@Injectable({
  providedIn: 'root'
})
export class SalePurchaseReportService {
  private readonly url = '/api/sale-purchase-report';

  constructor(private http: HttpClient) {}

  getReport(from?: string, to?: string, type?: SalePurchaseReportType, productId?: number): Observable<SalePurchaseReport> {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    if (type && type !== 'All') params = params.set('type', type);
    if (productId) params = params.set('productId', productId.toString());
    return this.http.get<SalePurchaseReport>(this.url, { params });
  }
}
