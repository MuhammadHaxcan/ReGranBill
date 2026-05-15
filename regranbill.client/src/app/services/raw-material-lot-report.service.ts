import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { RawMaterialLotReport, RawMaterialLotReportFilters } from '../models/raw-material-lot-report.model';

@Injectable({ providedIn: 'root' })
export class RawMaterialLotReportService {
  private readonly url = '/api/raw-material-lot-report';

  constructor(private http: HttpClient) {}

  getReport(filters: RawMaterialLotReportFilters): Observable<RawMaterialLotReport> {
    let params = new HttpParams();
    if (filters.from) params = params.set('from', filters.from);
    if (filters.to) params = params.set('to', filters.to);
    if (filters.vendorId) params = params.set('vendorId', filters.vendorId.toString());
    if (filters.productId) params = params.set('productId', filters.productId.toString());
    if (filters.lotNumber) params = params.set('lotNumber', filters.lotNumber);
    params = params.set('openOnly', String(filters.openOnly ?? false));
    params = params.set('includeDetails', String(filters.includeDetails ?? true));
    return this.http.get<RawMaterialLotReport>(this.url, { params });
  }
}
