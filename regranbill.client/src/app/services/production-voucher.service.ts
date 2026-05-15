import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import {
  ProductionVoucherApiDto,
  ProductionVoucherListDto,
  ProductionVoucherUpsertRequest
} from '../models/production-voucher.model';

@Injectable({ providedIn: 'root' })
export class ProductionVoucherService {
  private url = '/api/production-vouchers';

  constructor(private http: HttpClient) {}

  getAll(): Observable<ProductionVoucherListDto[]> {
    return this.http.get<ProductionVoucherListDto[]>(this.url);
  }

  getById(id: number): Observable<ProductionVoucherApiDto> {
    return this.http.get<ProductionVoucherApiDto>(`${this.url}/${id}`);
  }

  getNextNumber(): Observable<string> {
    return this.http.get<{ voucherNumber: string }>(`${this.url}/next-number`).pipe(
      map(res => res.voucherNumber)
    );
  }

  create(request: ProductionVoucherUpsertRequest): Observable<ProductionVoucherApiDto> {
    return this.http.post<ProductionVoucherApiDto>(this.url, request);
  }

  update(id: number, request: ProductionVoucherUpsertRequest): Observable<ProductionVoucherApiDto> {
    return this.http.put<ProductionVoucherApiDto>(`${this.url}/${id}`, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}
