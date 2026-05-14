import { Component, OnInit } from '@angular/core';
import { AuthenticatedPdfPageBase } from '../print-shared/authenticated-pdf-page.base';

@Component({
  selector: 'app-print-wsh',
  templateUrl: './print-wsh.component.html',
  styleUrl: './print-wsh.component.css',
  standalone: false
})
export class PrintWshComponent extends AuthenticatedPdfPageBase implements OnInit {
  ngOnInit(): void {
    const voucherKey = this.requireRouteParam('voucherKey', 'No washing voucher reference provided');
    if (!voucherKey) {
      return;
    }
    const isNumeric = /^\d+$/.test(voucherKey);
    const apiPath = isNumeric
      ? `/api/washing-vouchers/${voucherKey}/pdf`
      : `/api/washing-vouchers/by-number/${encodeURIComponent(voucherKey)}/pdf`;
    this.loadPdf(apiPath, `Print WSH - ${voucherKey}`);
  }
}
