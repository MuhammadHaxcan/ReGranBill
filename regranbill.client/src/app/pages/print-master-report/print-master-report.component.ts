import { Component, OnInit } from '@angular/core';
import { AuthenticatedPdfPageBase } from '../print-shared/authenticated-pdf-page.base';

@Component({
  selector: 'app-print-master-report',
  templateUrl: './print-master-report.component.html',
  styleUrl: './print-master-report.component.css',
  standalone: false
})
export class PrintMasterReportComponent extends AuthenticatedPdfPageBase implements OnInit {
  ngOnInit(): void {
    this.loadPdf(
      this.buildUrl('/api/master-report/pdf', {
        from: this.route.snapshot.queryParamMap.get('from'),
        to: this.route.snapshot.queryParamMap.get('to'),
        categoryId: this.route.snapshot.queryParamMap.get('categoryId'),
        accountId: this.route.snapshot.queryParamMap.get('accountId'),
        columns: this.route.snapshot.queryParamMap.get('columns')
      }),
      'Print Master Report'
    );
  }
}
