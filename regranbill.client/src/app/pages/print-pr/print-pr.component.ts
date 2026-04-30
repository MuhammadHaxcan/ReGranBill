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
    const id = this.requireRouteParam('id', 'No purchase return ID provided');
    if (!id) {
      return;
    }

    this.loadPdf(`/api/purchase-returns/${id}/pdf`, `Print PR - ${id}`);
  }
}
