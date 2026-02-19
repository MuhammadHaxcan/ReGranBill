import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { SaleInvoiceService } from '../../services/sale-invoice.service';
import { SaleInvoice, ProductLine, Cartage, Customer } from '../../models/sale-invoice.model';

@Component({
  selector: 'app-add-rate',
  templateUrl: './add-rate.component.html',
  styleUrl: './add-rate.component.css',
  standalone: false
})
export class AddRateComponent implements OnInit {
  invoiceId: number | null = null;
  invoiceNumber = '';
  invoiceDate = new Date();
  selectedCustomerId: number | null = null;
  description = '';
  status: 'Draft' | 'Posted' = 'Draft';

  customers: Customer[] = [];
  lines: ProductLine[] = [];

  cartage: Cartage | null = null;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private invoiceService: SaleInvoiceService
  ) {}

  ngOnInit(): void {
    this.customers = this.invoiceService.getCustomers();

    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.invoiceId = +idParam;
      const invoice = this.invoiceService.getInvoiceById(this.invoiceId);
      if (invoice) {
        this.invoiceNumber = invoice.invoiceNumber;
        this.invoiceDate = new Date(invoice.date);
        this.selectedCustomerId = invoice.customerId;
        this.description = invoice.description;
        this.status = invoice.status;
        this.lines = invoice.lines;
        this.cartage = invoice.cartage;
      }
    }
  }

  getCustomerName(): string {
    const customer = this.customers.find(c => c.id === this.selectedCustomerId);
    return customer ? `${customer.name} — ${customer.city}` : '—';
  }

  get formattedDate(): string {
    return this.invoiceDate.toLocaleDateString('en-GB', {
      day: '2-digit',
      month: 'short',
      year: 'numeric'
    });
  }

  getLineTotalWeight(line: ProductLine): number {
    if (!line.product || !line.qty) return 0;
    return line.product.packingWeightKg * line.qty;
  }

  getLineAmount(line: ProductLine): number {
    return this.getLineTotalWeight(line) * line.rate;
  }

  get totalBags(): number {
    return this.lines.reduce((sum, line) => sum + (line.qty || 0), 0);
  }

  get totalWeight(): number {
    return this.lines.reduce((sum, line) => sum + this.getLineTotalWeight(line), 0);
  }

  get totalAmount(): number {
    return this.lines.reduce((sum, line) => sum + this.getLineAmount(line), 0);
  }

  saveRates(): void {
    if (this.invoiceId) {
      const invoice: SaleInvoice = {
        id: this.invoiceId,
        invoiceNumber: this.invoiceNumber,
        date: this.invoiceDate,
        customerId: this.selectedCustomerId,
        description: this.description,
        lines: this.lines,
        cartage: this.cartage,
        status: this.status
      };
      this.invoiceService.updateInvoiceRates(this.invoiceId, invoice);
      alert('Rates saved successfully.');
      this.router.navigate(['/pending-invoices']);
    }
  }

  cancel(): void {
    this.router.navigate(['/pending-invoices']);
  }
}
