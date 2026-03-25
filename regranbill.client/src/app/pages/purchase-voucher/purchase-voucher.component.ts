import { Component, OnInit, HostListener, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { AccountService } from '../../services/account.service';
import { AuthService } from '../../services/auth.service';
import { PurchaseVoucherService } from '../../services/purchase-voucher.service';
import { ToastService } from '../../services/toast.service';
import { ConfirmModalService } from '../../services/confirm-modal.service';
import { Account } from '../../models/account.model';
import { ProductLine, Cartage } from '../../models/delivery-challan.model';
import { SelectOption } from '../../components/searchable-select/searchable-select.component';
import {
  PurchaseVoucherUpsertRequest,
  PurchaseVoucherViewModel
} from '../../models/purchase-voucher.model';
import { parseLocalDate, toDateInputValue } from '../../utils/date-utils';

@Component({
  selector: 'app-purchase-voucher',
  templateUrl: './purchase-voucher.component.html',
  styleUrl: './purchase-voucher.component.css',
  standalone: false
})
export class PurchaseVoucherComponent implements OnInit {
  challanId: number | null = null;
  isEditMode = false;
  voucherNumber = '';
  voucherDate = new Date();
  selectedVendorId: number | null = null;
  vehicleNumber = '';

  get voucherDateIso(): string {
    return toDateInputValue(this.voucherDate);
  }

  set voucherDateIso(val: string) {
    if (!val) return;
    this.voucherDate = parseLocalDate(val);
  }
  description = '';

  products: Account[] = [];
  vendors: Account[] = [];
  transporters: Account[] = [];
  lines: ProductLine[] = [];
  loading = true;

  // Dropdown options
  vendorOptions: SelectOption[] = [];
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
    private purchaseService: PurchaseVoucherService,
    private route: ActivatedRoute,
    private router: Router,
    private cdr: ChangeDetectorRef,
    private toast: ToastService,
    private confirmModal: ConfirmModalService
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
      vendors: this.accountService.getVendors(),
      transporters: this.accountService.getTransporters()
    }).subscribe({
      next: ({ products, vendors, transporters }) => {
        this.products = products;
        this.productOptions = products.map(p => ({
          value: p.id,
          label: p.name,
          sublabel: p.packing || ''
        }));

        this.vendors = vendors;
        this.vendorOptions = vendors.map(c => ({
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
        this.purchaseService.getById(this.challanId).subscribe({
        next: (voucher: PurchaseVoucherViewModel) => {
          this.voucherNumber = voucher.voucherNumber;
          this.voucherDate = parseLocalDate(voucher.date);
          this.selectedVendorId = voucher.vendorId;
          this.vehicleNumber = voucher.vehicleNumber || '';
          this.description = voucher.description || '';
          this.lines = voucher.lines.map(l => ({
            id: l.id,
            product: l.productId ? {
              id: l.productId,
              name: l.productName || '',
              packing: l.packing || '',
              packingWeightKg: l.packingWeightKg
            } : null,
            rbp: l.rbp,
            qty: l.qty,
            rate: l.rate,
            sortOrder: l.sortOrder
          }));
          this.cartage = voucher.cartage ? {
            transporterId: voucher.cartage.transporterId,
            transporterName: voucher.cartage.transporterName || '',
            city: voucher.cartage.city || '',
            amount: voucher.cartage.amount
          } : null;
          this.loading = false;
          this.cdr.detectChanges();
        },
        error: () => {
          this.toast.error('Unable to load purchase voucher.');
          this.loading = false;
          this.cdr.detectChanges();
        }
      });
    } else {
      this.purchaseService.getNextNumber().subscribe({
        next: num => {
          this.voucherNumber = num;
          this.addLine();
          this.loading = false;
          this.cdr.detectChanges();
        },
        error: () => {
          this.toast.error('Unable to get next voucher number.');
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
    return new Intl.DateTimeFormat('en-GB', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric'
    }).format(this.voucherDate);
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

  private buildRequest(): PurchaseVoucherUpsertRequest {
      return {
      date: this.voucherDate,
      vendorId: this.selectedVendorId!,
      vehicleNumber: this.vehicleNumber || null,
      description: this.description,
      lines: this.lines
        .filter(l => l.product)
        .map((l, i) => ({
          productId: l.product!.id,
          rbp: l.rbp,
          qty: this.toNumber(l.qty),
          rate: this.toNumber(l.rate),
          sortOrder: i
        })),
      cartage: this.cartage ? {
        transporterId: this.cartage.transporterId,
        amount: this.toNumber(this.cartage.amount)
      } : null
    };
  }

  get validLines(): ProductLine[] {
    return this.lines.filter(l => l.product && l.qty > 0);
  }

  get canSave(): boolean {
    return !!this.selectedVendorId && this.validLines.length > 0;
  }

  private validate(): boolean {
    if (!this.selectedVendorId) {
      this.toast.error('Please select a vendor.');
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

  async discard(): Promise<void> {
    const confirmed = await this.confirmModal.confirm({
      title: 'Discard Voucher',
      message: 'Are you sure you want to discard this purchase voucher? All unsaved changes will be lost.',
      confirmText: 'Discard',
      cancelText: 'Cancel'
    });
    if (confirmed) this.resetForm();
  }

  save(): void {
    if (!this.validate()) return;
    const req = this.buildRequest();

    if (this.isEditMode && this.challanId) {
      this.purchaseService.update(this.challanId, req).subscribe({
        next: () => {
          this.toast.success('Purchase voucher updated successfully.');
          this.router.navigate(['/pending-purchases']);
        },
        error: err => {
          this.toast.error(err?.error?.message || 'Unable to save purchase voucher.');
        }
      });
    } else {
      this.purchaseService.create(req).subscribe({
        next: dc => {
          this.toast.success(`${dc.voucherNumber} created successfully.`);
          this.resetForm();
        },
        error: err => {
          this.toast.error(err?.error?.message || 'Unable to create purchase voucher.');
        }
      });
    }
  }

  saveAndPrint(): void {
    if (!this.validate()) return;
    const req = this.buildRequest();

    if (this.isEditMode && this.challanId) {
      this.purchaseService.update(this.challanId, req).subscribe({
        next: dc => {
          this.toast.success('Purchase voucher updated successfully.');
          this.purchaseService.openPdfInNewTab(dc.id);
          this.router.navigate(['/pending-purchases']);
        },
        error: err => {
          this.toast.error(err?.error?.message || 'Unable to save purchase voucher.');
        }
      });
    } else {
      this.purchaseService.create(req).subscribe({
        next: dc => {
          this.toast.success(`${dc.voucherNumber} created successfully.`);
          this.purchaseService.openPdfInNewTab(dc.id);
          this.resetForm();
        },
        error: err => {
          this.toast.error(err?.error?.message || 'Unable to create purchase voucher.');
        }
      });
    }
  }

  private resetForm(): void {
    this.selectedVendorId = null;
    this.vehicleNumber = '';
    this.description = '';
    this.voucherDate = new Date();
    this.cartage = null;
    this.showCartageForm = false;
    this.lines = [];
    this.addLine();
    this.purchaseService.getNextNumber().subscribe({
      next: num => {
        this.voucherNumber = num;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to get next voucher number.');
      }
    });
  }

  private toNumber(value: unknown): number {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
  }
}
