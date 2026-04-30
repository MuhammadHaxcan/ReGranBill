import { Component, OnInit } from '@angular/core';
import { AuthenticatedPdfPageBase } from '../print-shared/authenticated-pdf-page.base';

@Component({
  selector: 'app-print-customer-ledger',
  templateUrl: './print-customer-ledger.component.html',
  styleUrl: './print-customer-ledger.component.css',
  standalone: false
})
export class PrintCustomerLedgerComponent extends AuthenticatedPdfPageBase implements OnInit {
  ngOnInit(): void {
    const accountId = this.requireRouteParam('accountId', 'No account ID provided');
    if (!accountId) {
      return;
    }

    this.loadPdf(
      this.buildUrl(`/api/customer-ledger/${accountId}/pdf`, {
        fromDate: this.route.snapshot.queryParamMap.get('fromDate'),
        toDate: this.route.snapshot.queryParamMap.get('toDate')
      }),
      `Print Ledger - ${accountId}`
    );
  }
}
