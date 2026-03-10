import { Component, OnInit, HostListener, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { AccountService } from '../../services/account.service';
import { AuthService } from '../../services/auth.service';
import { DeliveryChallanService } from '../../services/delivery-challan.service';
import { ToastService } from '../../services/toast.service';
import { Account } from '../../models/account.model';
import { ProductLine, Cartage } from '../../models/delivery-challan.model';
import { SelectOption } from '../../components/searchable-select/searchable-select.component';

@Component({
  selector: 'app-delivery-challan',
  templateUrl: './delivery-challan.component.html',
  styleUrl: './delivery-challan.component.css',
  standalone: false
})
export class DeliveryChallanComponent implements OnInit {
  challanId: number | null = null;
  isEditMode = false;
  dcNumber = '';
  dcDate = new Date();
  selectedCustomerId: number | null = null;
  vehicleNumber = '';

  get dcDateIso(): string {
    const y = this.dcDate.getFullYear();
    const m = String(this.dcDate.getMonth() + 1).padStart(2, '0');
    const d = String(this.dcDate.getDate()).padStart(2, '0');
    return `${y}-${m}-${d}`;
  }

  set dcDateIso(val: string) {
    if (!val) return;
    const [y, m, d] = val.split('-').map(Number);
    this.dcDate = new Date(y, m - 1, d);
  }
  description = '';

  products: Account[] = [];
  customers: Account[] = [];
  transporters: Account[] = [];
  lines: ProductLine[] = [];
  loading = true;

  // Dropdown options
  customerOptions: SelectOption[] = [];
  productOptions: SelectOption[] = [];
  transporterOptions: SelectOption[] = [];
  rbpOptions: SelectOption[] = [
    { value: 'Yes', label: 'Yes' },
    { value: 'No', label: 'No' }
  ];

  // Cartage
  cartage: Cartage | null = null;
  showCartageForm = false;
  cartageTransporterId: number | null = null;
  cartageAmount: number = 0;

  constructor(
    private accountService: AccountService,
    private authService: AuthService,
    private dcService: DeliveryChallanService,
    private route: ActivatedRoute,
    private router: Router,
    private cdr: ChangeDetectorRef,
    private toast: ToastService
  ) {}

  get isAdmin(): boolean {
    return this.authService.isAdmin();
  }

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading = true;
    forkJoin({
      products: this.accountService.getProducts(),
      customers: this.accountService.getCustomers(),
      transporters: this.accountService.getTransporters()
    }).subscribe({
      next: ({ products, customers, transporters }) => {
        this.products = products;
        this.productOptions = products.map(p => ({
          value: p.id,
          label: p.name,
          sublabel: p.packing || ''
        }));

        this.customers = customers;
        this.customerOptions = customers.map(c => ({
          value: c.id,
          label: c.name,
          sublabel: c.city || ''
        }));

        this.transporters = transporters;
        this.transporterOptions = transporters.map(t => ({
          value: t.id,
          label: t.name,
          sublabel: t.city || ''
        }));

        this.loadChallan();
      },
      error: () => {
        this.toast.error('Unable to load form data.');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  loadChallan(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.challanId = +idParam;
      this.isEditMode = true;
      this.dcService.getById(this.challanId).subscribe({
        next: dc => {
          if (dc) {
            this.dcNumber = dc.dcNumber;
            this.dcDate = new Date(dc.date);
            this.selectedCustomerId = dc.customerId;
            this.vehicleNumber = dc.vehicleNumber || '';
            this.description = dc.description || '';
            this.lines = dc.lines.map((l: any) => ({
              id: l.id,
              product: l.productId ? {
                id: l.productId,
                name: l.productName || '',
                packing: l.packing || '',
                packingWeightKg: l.packingWeightKg
              } : null,
              rbp: l.rbp as 'Yes' | 'No',
              qty: l.qty,
              rate: l.rate,
              sortOrder: l.sortOrder
            }));
            this.cartage = dc.cartage ? {
              transporterId: dc.cartage.transporterId,
              transporterName: dc.cartage.transporterName || '',
              city: dc.cartage.city || '',
              amount: dc.cartage.amount
            } : null;
          }
          this.loading = false;
          this.cdr.detectChanges();
        },
        error: () => {
          this.toast.error('Unable to load delivery challan.');
          this.loading = false;
          this.cdr.detectChanges();
        }
      });
    } else {
      this.dcService.getNextNumber().subscribe({
        next: num => {
          this.dcNumber = num;
          this.addLine();
          this.loading = false;
          this.cdr.detectChanges();
        },
        error: () => {
          this.toast.error('Unable to get next challan number.');
          this.loading = false;
          this.cdr.detectChanges();
        }
      });
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
    this.lines.push({ product: null, rbp: 'Yes', qty: 0, rate: 0 });
  }

  removeLine(index: number): void {
    if (this.lines.length > 1) {
      this.lines.splice(index, 1);
    }
  }

  onProductChange(line: ProductLine, productId: number): void {
    const acct = this.products.find(p => p.id === productId);
    if (acct) {
      line.product = {
        id: acct.id,
        name: acct.name,
        packing: acct.packing!,
        packingWeightKg: acct.packingWeightKg!
      };
    } else {
      line.product = null;
    }
  }

  onRbpChange(line: ProductLine, val: string): void {
    line.rbp = val as 'Yes' | 'No';
  }

  getLineTotalWeight(line: ProductLine): number {
    if (!line.product || !line.qty) return 0;
    if (line.rbp === 'No') return line.qty;
    return line.product.packingWeightKg * line.qty;
  }

  isLoose(line: ProductLine): boolean {
    return line.rbp === 'No';
  }

  getLineAmount(line: ProductLine): number {
    if (!line.product || !line.qty) return 0;
    if (line.rbp === 'Yes') {
      return line.product.packingWeightKg * line.qty * line.rate;
    }
    return line.qty * line.rate;
  }

  get totalBags(): number {
    return this.lines
      .filter(l => l.rbp === 'Yes')
      .reduce((sum, l) => sum + (l.qty || 0), 0);
  }

  get totalWeight(): number {
    return this.lines.reduce((sum, l) => sum + this.getLineTotalWeight(l), 0);
  }

  get totalAmount(): number {
    return this.lines.reduce((sum, l) => sum + this.getLineAmount(l), 0);
  }

  get formattedDate(): string {
    return this.dcDate.toLocaleDateString('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric'
    });
  }

  // Cartage
  openCartageForm(): void {
    if (this.cartage) {
      this.cartageTransporterId = this.cartage.transporterId;
      this.cartageAmount = this.cartage.amount;
    } else {
      this.cartageTransporterId = null;
      this.cartageAmount = 0;
    }
    this.showCartageForm = true;
  }

  saveCartage(): void {
    if (this.cartageTransporterId === null) return;
    const t = this.transporters.find(x => x.id === this.cartageTransporterId);
    if (!t) return;
    this.cartage = {
      transporterId: t.id,
      transporterName: t.name,
      city: t.city || '',
      amount: this.cartageAmount
    };
    this.showCartageForm = false;
  }

  cancelCartageForm(): void {
    this.showCartageForm = false;
  }

  removeCartage(): void {
    this.cartage = null;
  }

  private buildRequest() {
    return {
      date: this.dcDate,
      customerId: this.selectedCustomerId!,
      vehicleNumber: this.vehicleNumber || null,
      description: this.description,
      lines: this.lines
        .filter(l => l.product)
        .map((l, i) => ({
          productId: l.product!.id,
          rbp: l.rbp,
          qty: l.qty,
          rate: l.rate,
          sortOrder: i
        })),
      cartage: this.cartage ? {
        transporterId: this.cartage.transporterId,
        amount: this.cartage.amount
      } : null
    };
  }

  get validLines(): any[] {
    return this.lines.filter(l => l.product && l.qty > 0);
  }

  get canSave(): boolean {
    return !!this.selectedCustomerId && this.validLines.length > 0;
  }

  private validate(): boolean {
    if (!this.selectedCustomerId) {
      this.toast.error('Please select a customer.');
      return false;
    }
    const linesWithProduct = this.lines.filter(l => l.product);
    if (linesWithProduct.length === 0) {
      this.toast.error('Please add at least one line item with a product.');
      return false;
    }
    if (linesWithProduct.some(l => !l.qty || l.qty <= 0)) {
      this.toast.error('All line items must have a quantity greater than zero.');
      return false;
    }
    return true;
  }

  discard(): void {
    if (confirm('Are you sure you want to discard this delivery challan?')) {
      this.resetForm();
    }
  }

  save(): void {
    if (!this.validate()) return;
    const req = this.buildRequest();

    if (this.isEditMode && this.challanId) {
      this.dcService.update(this.challanId, req).subscribe({
        next: () => {
          this.toast.success('Challan updated successfully.');
          this.router.navigate(['/pending-challans']);
        },
        error: err => {
          this.toast.error(err?.error?.message || 'Unable to save delivery challan.');
        }
      });
    } else {
      this.dcService.create(req).subscribe({
        next: dc => {
          this.toast.success(`${dc.dcNumber} created successfully.`);
          this.resetForm();
        },
        error: err => {
          this.toast.error(err?.error?.message || 'Unable to create delivery challan.');
        }
      });
    }
  }

  saveAndPrint(): void {
    if (!this.validate()) return;
    const req = this.buildRequest();

    if (this.isEditMode && this.challanId) {
      this.dcService.update(this.challanId, req).subscribe({
        next: dc => {
          this.toast.success('Challan updated successfully.');
          this.dcService.openPdfInNewTab(dc.id);
          this.router.navigate(['/pending-challans']);
        },
        error: err => {
          this.toast.error(err?.error?.message || 'Unable to save delivery challan.');
        }
      });
    } else {
      this.dcService.create(req).subscribe({
        next: dc => {
          this.toast.success(`${dc.dcNumber} created successfully.`);
          this.dcService.openPdfInNewTab(dc.id);
          this.resetForm();
        },
        error: err => {
          this.toast.error(err?.error?.message || 'Unable to create delivery challan.');
        }
      });
    }
  }

  private resetForm(): void {
    this.selectedCustomerId = null;
    this.vehicleNumber = '';
    this.description = '';
    this.dcDate = new Date();
    this.cartage = null;
    this.showCartageForm = false;
    this.lines = [];
    this.addLine();
    this.dcService.getNextNumber().subscribe({
      next: num => {
        this.dcNumber = num;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to get next challan number.');
      }
    });
  }
}
