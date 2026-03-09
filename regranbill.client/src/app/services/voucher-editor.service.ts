import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { UpdateVoucherEditorRequest, VoucherEditorVoucher, VoucherType } from '../models/voucher-editor.model';

@Injectable({
  providedIn: 'root',
})
export class VoucherEditorService {
  private url = '/api/voucher-editor';

  constructor(private http: HttpClient) {}

  search(voucherType: VoucherType, voucherNumber: string): Observable<VoucherEditorVoucher> {
    const params = new HttpParams()
      .set('voucherType', voucherType)
      .set('voucherNumber', voucherNumber.trim());

    return this.http.get<VoucherEditorVoucher>(`${this.url}/search`, { params });
  }

  update(request: UpdateVoucherEditorRequest): Observable<VoucherEditorVoucher> {
    return this.http.put<VoucherEditorVoucher>(this.url, request);
  }
}
