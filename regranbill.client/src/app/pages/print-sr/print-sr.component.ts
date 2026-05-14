import { Component, OnInit } from '@angular/core';
import { AuthenticatedPdfPageBase } from '../print-shared/authenticated-pdf-page.base';

@Component({
  selector: 'app-print-sr',
  templateUrl: './print-sr.component.html',
  styleUrl: './print-sr.component.css',
  standalone: false
})
export class PrintSrComponent extends AuthenticatedPdfPageBase implements OnInit {
  ngOnInit(): void {
    const voucherKey = this.requireRouteParam('voucherKey', 'No sale return reference provided');
    if (!voucherKey) {
      return;
    }
    const isNumeric = /^\d+$/.test(voucherKey);
    const apiPath = isNumeric
      ? `/api/sale-returns/${voucherKey}/pdf`
      : `/api/sale-returns/by-number/${encodeURIComponent(voucherKey)}/pdf`;
    this.loadPdf(apiPath, `Print SR - ${voucherKey}`);
  }
}
