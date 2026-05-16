import { Component, OnInit } from '@angular/core';
import { AuthenticatedPdfPageBase } from '../print-shared/authenticated-pdf-page.base';

@Component({
  selector: 'app-print-prod',
  templateUrl: './print-prod.component.html',
  styleUrl: './print-prod.component.css',
  standalone: false
})
export class PrintProdComponent extends AuthenticatedPdfPageBase implements OnInit {
  ngOnInit(): void {
    const voucherKey = this.requireRouteParam('voucherKey', 'No production voucher reference provided');
    if (!voucherKey) {
      return;
    }
    const isNumeric = /^\d+$/.test(voucherKey);
    const apiPath = isNumeric
      ? `/api/production-vouchers/${voucherKey}/pdf`
      : `/api/production-vouchers/by-number/${encodeURIComponent(voucherKey)}/pdf`;
    this.loadPdf(apiPath, `Print PROD - ${voucherKey}`);
  }
}
