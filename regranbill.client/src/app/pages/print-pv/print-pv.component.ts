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
    const id = this.requireRouteParam('id', 'No purchase voucher ID provided');
    if (!id) {
      return;
    }

    this.loadPdf(`/api/purchase-vouchers/${id}/pdf`, `Print PV - ${id}`);
  }
}
