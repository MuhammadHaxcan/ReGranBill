import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { SaleInvoiceService } from '../../services/sale-invoice.service';
import { SaleInvoice, ProductLine } from '../../models/sale-invoice.model';

@Component({
  selector: 'app-pending-invoices',
  templateUrl: './pending-invoices.component.html',
  styleUrl: './pending-invoices.component.css',
  standalone: false
})
export class PendingInvoicesComponent implements OnInit {
  invoices: SaleInvoice[] = [];

  constructor(
    private invoiceService: SaleInvoiceService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadInvoices();
  }

  loadInvoices(): void {
    this.invoices = this.invoiceService.getAllInvoices();
  }

  getTotalBags(invoice: SaleInvoice): number {
    return invoice.lines.reduce((sum, line) => sum + (line.qty || 0), 0);
  }

  getTotalWeight(invoice: SaleInvoice): number {
    return invoice.lines.reduce((sum, line) => {
      if (!line.product || !line.qty) return sum;
      return sum + (line.product.packingWeightKg * line.qty);
    }, 0);
  }

  getTotalAmount(invoice: SaleInvoice): number {
    return invoice.lines.reduce((sum, line) => {
      if (!line.product || !line.qty) return sum;
      return sum + (line.product.packingWeightKg * line.qty * line.rate);
    }, 0);
  }

  hasRates(invoice: SaleInvoice): boolean {
    return invoice.lines.some(line => line.rate > 0);
  }

  getFormattedDate(date: Date): string {
    return new Date(date).toLocaleDateString('en-GB', {
      day: '2-digit',
      month: 'short',
      year: 'numeric'
    });
  }

  getProductCount(invoice: SaleInvoice): number {
    return invoice.lines.length;
  }

  editInvoice(invoice: SaleInvoice): void {
    this.router.navigate(['/sale-invoice', invoice.id]);
  }

  addRate(invoice: SaleInvoice): void {
    this.router.navigate(['/add-rate', invoice.id]);
  }
}
