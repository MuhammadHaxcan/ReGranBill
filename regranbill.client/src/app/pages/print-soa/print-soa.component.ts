import { Component, OnInit } from '@angular/core';
import { AuthenticatedPdfPageBase } from '../print-shared/authenticated-pdf-page.base';

@Component({
  selector: 'app-print-soa',
  templateUrl: './print-soa.component.html',
  styleUrl: './print-soa.component.css',
  standalone: false
})
export class PrintSoaComponent extends AuthenticatedPdfPageBase implements OnInit {
  ngOnInit(): void {
    const accountId = this.requireRouteParam('accountId', 'No statement account ID provided');
    if (!accountId) {
      return;
    }

    this.loadPdf(
      this.buildUrl(`/api/statements/${accountId}/pdf`, {
        fromDate: this.route.snapshot.queryParamMap.get('fromDate'),
        toDate: this.route.snapshot.queryParamMap.get('toDate')
      }),
      `Print SOA - ${accountId}`
    );
  }
}
