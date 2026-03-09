import { Component, OnInit, HostListener, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { AccountService } from '../../services/account.service';
import { AuthService } from '../../services/auth.service';
import { DeliveryChallanService } from '../../services/delivery-challan.service';
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

  // Toast
  toastVisible = false;
  toastMessage = '';
  toastType: 'success' | 'error' = 'success';

  get dcDateStr(): string {
    const d = this.dcDate;
    const dd = String(d.getDate()).padStart(2, '0');
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const yyyy = d.getFullYear();
    return `${dd}/${mm}/${yyyy}`;
  }

  set dcDateStr(val: string) {
    const parts = val.replace(/[-\.]/g, '/').split('/');
    if (parts.length === 3) {
      const day = parseInt(parts[0], 10);
      const month = parseInt(parts[1], 10) - 1;
      const year = parseInt(parts[2], 10);
      if (!isNaN(day) && !isNaN(month) && !isNaN(year) && year > 1900) {
        this.dcDate = new Date(year, month, day);
      }
    }
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
    private cdr: ChangeDetectorRef
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
    }).subscribe(({ products, customers, transporters }) => {
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
    });
  }

  loadChallan(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.challanId = +idParam;
      this.isEditMode = true;
      this.dcService.getById(this.challanId).subscribe(dc => {
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
      });
    } else {
      this.dcService.getNextNumber().subscribe(num => {
        this.dcNumber = num;
        this.addLine();
        this.loading = false;
        this.cdr.detectChanges();
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
    return this.lines.reduce((sum, l) => sum + (l.qty || 0), 0);
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

  discard(): void {
    if (confirm('Are you sure you want to discard this delivery challan?')) {
      this.resetForm();
    }
  }

  save(): void {
    const req = this.buildRequest();

    if (this.isEditMode && this.challanId) {
      this.dcService.update(this.challanId, req).subscribe(() => {
        this.showToast('Challan saved successfully');
        this.router.navigate(['/pending-challans']);
      });
    } else {
      this.dcService.create(req).subscribe(dc => {
        this.showToast(`${dc.dcNumber} saved successfully`);
        this.resetForm();
      });
    }
  }

  saveAndPrint(): void {
    const req = this.buildRequest();

    if (this.isEditMode && this.challanId) {
      this.dcService.update(this.challanId, req).subscribe(dc => {
        this.showToast('Challan saved successfully');
        this.dcService.openPdfInNewTab(dc.id);
        this.router.navigate(['/pending-challans']);
      });
    } else {
      this.dcService.create(req).subscribe(dc => {
        this.showToast(`${dc.dcNumber} saved successfully`);
        this.dcService.openPdfInNewTab(dc.id);
        this.resetForm();
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
    this.dcService.getNextNumber().subscribe(num => {
      this.dcNumber = num;
      this.cdr.detectChanges();
    });
  }

  showToast(message: string, type: 'success' | 'error' = 'success'): void {
    this.toastMessage = message;
    this.toastType = type;
    this.toastVisible = true;
    this.cdr.detectChanges();
    setTimeout(() => {
      this.toastVisible = false;
      this.cdr.detectChanges();
    }, 3500);
  }
}
