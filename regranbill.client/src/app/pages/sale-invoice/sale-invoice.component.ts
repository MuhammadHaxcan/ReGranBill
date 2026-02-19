import { Component, OnInit, OnDestroy, HostListener } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { SaleInvoiceService } from '../../services/sale-invoice.service';
import { Product, ProductLine, Cartage, Customer } from '../../models/sale-invoice.model';

@Component({
  selector: 'app-sale-invoice',
  templateUrl: './sale-invoice.component.html',
  styleUrl: './sale-invoice.component.css',
  standalone: false
})
export class SaleInvoiceComponent implements OnInit {
  invoiceId: number | null = null;
  isEditMode = false;
  invoiceNumber = '';
  invoiceDate = new Date();
  selectedCustomerId: number | null = null;
  description = '';
  status: 'Draft' | 'Posted' = 'Draft';

  products: Product[] = [];
  customers: Customer[] = [];
  lines: ProductLine[] = [];

  cartage: Cartage | null = null;
  showCartageForm = false;
  cartageForm: Cartage = { providerName: '', city: '', amount: 0 };

  constructor(
    private invoiceService: SaleInvoiceService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.products = this.invoiceService.getProducts();
    this.customers = this.invoiceService.getCustomers();

    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.invoiceId = +idParam;
      this.isEditMode = true;
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
    } else {
      this.invoiceNumber = this.invoiceService.getNextInvoiceNumber();

      // Start with two default lines matching the screenshot
      this.addLine();
      this.addLine();

      // Pre-fill demo data matching screenshot
      this.lines[0].product = this.products[0]; // HDPE Blue Drum
      this.lines[0].rbp = 'Yes';
      this.lines[0].qty = 2;
      this.lines[0].rate = 50;

      this.lines[1].product = this.products[1]; // PP-125 Natural
      this.lines[1].rbp = 'Yes';
      this.lines[1].qty = 5;
      this.lines[1].rate = 180.5;

      // Pre-fill cartage
      this.cartage = {
        providerName: 'Al-Madina Logistics',
        city: 'Multan',
        amount: 200
      };
    }
  }

  @HostListener('document:keydown', ['$event'])
  handleKeyboardShortcut(event: KeyboardEvent): void {
    if (event.altKey && event.key === 'n') {
      event.preventDefault();
      this.addLine();
    }
  }

  addLine(): void {
    this.lines.push({
      product: null,
      rbp: 'Yes',
      qty: 0,
      rate: 0
    });
  }

  removeLine(index: number): void {
    if (this.lines.length > 1) {
      this.lines.splice(index, 1);
    }
  }

  onProductChange(line: ProductLine, productId: number): void {
    line.product = this.products.find(p => p.id === productId) || null;
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

  get formattedDate(): string {
    return this.invoiceDate.toLocaleDateString('en-GB', {
      day: '2-digit',
      month: 'short',
      year: 'numeric'
    });
  }

  // Cartage management
  openCartageForm(): void {
    if (this.cartage) {
      this.cartageForm = { ...this.cartage };
    } else {
      this.cartageForm = { providerName: '', city: '', amount: 0 };
    }
    this.showCartageForm = true;
  }

  saveCartage(): void {
    this.cartage = { ...this.cartageForm };
    this.showCartageForm = false;
  }

  cancelCartageForm(): void {
    this.showCartageForm = false;
  }

  removeCartage(): void {
    this.cartage = null;
  }

  // Invoice actions
  discard(): void {
    if (confirm('Are you sure you want to discard this invoice?')) {
      this.lines = [];
      this.addLine();
      this.selectedCustomerId = null;
      this.description = '';
      this.cartage = null;
      this.invoiceNumber = this.invoiceService.getNextInvoiceNumber();
    }
  }

  saveDraft(): void {
    this.invoiceService.saveDraft({
      id: this.invoiceId ?? undefined,
      invoiceNumber: this.invoiceNumber,
      date: this.invoiceDate,
      customerId: this.selectedCustomerId,
      description: this.description,
      lines: this.lines,
      cartage: this.cartage,
      status: 'Draft'
    });
    alert('Draft saved successfully.');
    if (this.isEditMode) {
      this.router.navigate(['/pending-invoices']);
    }
  }

  submitAndPost(): void {
    this.invoiceService.submitInvoice({
      id: this.invoiceId ?? undefined,
      invoiceNumber: this.invoiceNumber,
      date: this.invoiceDate,
      customerId: this.selectedCustomerId,
      description: this.description,
      lines: this.lines,
      cartage: this.cartage,
      status: 'Posted'
    });
    this.status = 'Posted';
    alert('Invoice submitted and posted.');
    if (this.isEditMode) {
      this.router.navigate(['/pending-invoices']);
    }
  }
}
