import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ProductStockReport, ProductStockReportFilters } from '../models/product-stock-report.model';

@Injectable({
  providedIn: 'root'
})
export class ProductStockReportService {
  private readonly url = '/api/product-stock-report';

  constructor(private http: HttpClient) {}

  getReport(filters: ProductStockReportFilters): Observable<ProductStockReport> {
    let params = new HttpParams();

    if (filters.from) params = params.set('from', filters.from);
    if (filters.to) params = params.set('to', filters.to);
    if (filters.categoryId) params = params.set('categoryId', filters.categoryId.toString());
    if (filters.productId) params = params.set('productId', filters.productId.toString());
    params = params.set('includeDetails', String(filters.includeDetails ?? false));

    return this.http.get<ProductStockReport>(this.url, { params });
  }
}
