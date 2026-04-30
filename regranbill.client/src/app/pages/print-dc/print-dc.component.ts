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
    const id = this.requireRouteParam('id', 'No challan ID provided');
    if (!id) {
      return;
    }

    this.loadPdf(`/api/delivery-challans/${id}/pdf`, `Print DC - ${id}`);
  }
}
