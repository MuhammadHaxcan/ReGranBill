import { Component, OnInit } from '@angular/core';
import { AuthenticatedPdfPageBase } from '../print-shared/authenticated-pdf-page.base';

@Component({
  selector: 'app-print-dc',
  templateUrl: './print-dc.component.html',
  styleUrl: './print-dc.component.css',
  standalone: false
})
export class PrintDcComponent extends AuthenticatedPdfPageBase implements OnInit {
  ngOnInit(): void {
    const voucherKey = this.requireRouteParam('voucherKey', 'No challan reference provided');
    if (!voucherKey) {
      return;
    }
    const isNumeric = /^\d+$/.test(voucherKey);
    const apiPath = isNumeric
      ? `/api/delivery-challans/${voucherKey}/pdf`
      : `/api/delivery-challans/by-number/${encodeURIComponent(voucherKey)}/pdf`;
    this.loadPdf(apiPath, `Print DC - ${voucherKey}`);
  }
}
