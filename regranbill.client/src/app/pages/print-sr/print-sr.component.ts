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
    const id = this.requireRouteParam('id', 'No sale return ID provided');
    if (!id) {
      return;
    }

    this.loadPdf(`/api/sale-returns/${id}/pdf`, `Print SR - ${id}`);
  }
}
