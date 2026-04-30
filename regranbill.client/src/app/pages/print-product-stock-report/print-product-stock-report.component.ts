import { Component, OnInit } from '@angular/core';
import { AuthenticatedPdfPageBase } from '../print-shared/authenticated-pdf-page.base';

@Component({
  selector: 'app-print-product-stock-report',
  templateUrl: './print-product-stock-report.component.html',
  styleUrl: './print-product-stock-report.component.css',
  standalone: false
})
export class PrintProductStockReportComponent extends AuthenticatedPdfPageBase implements OnInit {
  ngOnInit(): void {
    this.loadPdf(
      this.buildUrl('/api/product-stock-report/pdf', {
        from: this.route.snapshot.queryParamMap.get('from'),
        to: this.route.snapshot.queryParamMap.get('to'),
        categoryId: this.route.snapshot.queryParamMap.get('categoryId'),
        productId: this.route.snapshot.queryParamMap.get('productId'),
        selectedMovementProductId: this.route.snapshot.queryParamMap.get('selectedMovementProductId'),
        includeDetails: 'true'
      }),
      'Print Product Stock Report'
    );
  }
}
