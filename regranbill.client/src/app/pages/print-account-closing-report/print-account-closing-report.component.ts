import { Component, OnInit } from '@angular/core';
import { AuthenticatedPdfPageBase } from '../print-shared/authenticated-pdf-page.base';

@Component({
  selector: 'app-print-account-closing-report',
  templateUrl: './print-account-closing-report.component.html',
  styleUrl: './print-account-closing-report.component.css',
  standalone: false
})
export class PrintAccountClosingReportComponent extends AuthenticatedPdfPageBase implements OnInit {
  ngOnInit(): void {
    this.loadPdf(
      this.buildUrl('/api/account-closing-report/pdf', {
        from: this.route.snapshot.queryParamMap.get('from'),
        to: this.route.snapshot.queryParamMap.get('to'),
        accountId: this.route.snapshot.queryParamMap.get('accountId'),
        historyAccountId: this.route.snapshot.queryParamMap.get('historyAccountId')
      }),
      'Print Account Closing Report'
    );
  }
}
