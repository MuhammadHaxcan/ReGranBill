import { Component, OnInit } from '@angular/core';
import { AuthenticatedPdfPageBase } from '../print-shared/authenticated-pdf-page.base';

@Component({
  selector: 'app-print-pr',
  templateUrl: './print-pr.component.html',
  styleUrl: './print-pr.component.css',
  standalone: false
})
export class PrintPrComponent extends AuthenticatedPdfPageBase implements OnInit {
  ngOnInit(): void {
    const voucherKey = this.requireRouteParam('voucherKey', 'No purchase return reference provided');
    if (!voucherKey) {
      return;
    }
    const isNumeric = /^\d+$/.test(voucherKey);
    const apiPath = isNumeric
      ? `/api/purchase-returns/${voucherKey}/pdf`
      : `/api/purchase-returns/by-number/${encodeURIComponent(voucherKey)}/pdf`;
    this.loadPdf(apiPath, `Print PR - ${voucherKey}`);
  }
}
