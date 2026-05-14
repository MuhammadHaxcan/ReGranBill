import { Component, OnInit } from '@angular/core';
import { AuthenticatedPdfPageBase } from '../print-shared/authenticated-pdf-page.base';

@Component({
  selector: 'app-print-pv',
  templateUrl: './print-pv.component.html',
  styleUrl: './print-pv.component.css',
  standalone: false
})
export class PrintPvComponent extends AuthenticatedPdfPageBase implements OnInit {
  ngOnInit(): void {
    const voucherKey = this.requireRouteParam('voucherKey', 'No purchase voucher reference provided');
    if (!voucherKey) {
      return;
    }
    const isNumeric = /^\d+$/.test(voucherKey);
    const apiPath = isNumeric
      ? `/api/purchase-vouchers/${voucherKey}/pdf`
      : `/api/purchase-vouchers/by-number/${encodeURIComponent(voucherKey)}/pdf`;
    this.loadPdf(apiPath, `Print PV - ${voucherKey}`);
  }
}
