import { Component, OnInit, HostListener, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { catchError, finalize, forkJoin, of } from 'rxjs';
import { AccountService } from '../../services/account.service';
import { AuthService } from '../../services/auth.service';
import { PurchaseReturnService } from '../../services/purchase-return.service';
import { CategoryService } from '../../services/category.service';
import { CompanySettingsService } from '../../services/company-settings.service';
import { ToastService } from '../../services/toast.service';
import { ConfirmModalService } from '../../services/confirm-modal.service';
import { Account } from '../../models/account.model';
import { Category } from '../../models/category.model';
import { SelectOption } from '../../components/searchable-select/searchable-select.component';
import { VehicleOption } from '../../models/company-settings.model';
import { formatDateDisplay, parseLocalDate, toDateInputValue } from '../../utils/date-utils';
import { getPurchaseLineWeight, getPurchaseLineAmount, getPurchaseTotalBags, getPurchaseTotalWeight, getPurchaseTotalAmount } from '../../utils/delivery-calculations';
import { getApiErrorMessage } from '../../utils/api-error';

@Component({
  selector: 'app-purchase-return',
  templateUrl: './purchase-return.component.html',
  styleUrl: './purchase-return.component.css',
  standalone: false
})
export class PurchaseReturnComponent implements OnInit {
  purchaseReturnId: number | null = null;
  isEditMode = false;
  prNumber = '';
  purchaseReturnDate = new Date();

  selectedVendorId: number | null = null;
  vehicleNumber = '';
  description = '';

  products: Account[] = [];
  vendors: Account[] = [];
  vehicleOptions: VehicleOption[] = [];
  lines: PurchaseReturnLine[] = [];
  loading = true;
  private latestRatesByProductId = new Map<number, number>();
  isReadOnlyRatedVoucher = false;

  vendorOptions: SelectOption[] = [];
  productOptions: SelectOption[] = [];
  categoryOptions: SelectOption[] = [];

  // Category filter per line
  lineCategoryIds: (number | null)[] = [];
  vehicleSelectOptions: SelectOption[] = [];

  constructor(
    private accountService: AccountService,
    private authService: AuthService,
    private purchaseReturnService: PurchaseReturnService,
    private categoryService: CategoryService,
    private companySettingsService: CompanySettingsService,
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
      vehicles: this.companySettingsService.getVehicles().pipe(catchError(() => of([]))),
      categories: this.categoryService.getAll()
    }).subscribe({
      next: ({ products, vendors, vehicles, categories }) => {
        this.products = products;
        this.productOptions = products.map(p => ({
          value: p.id,
          label: p.name,
          sublabel: p.packing || ''
        }));

        this.vendors = vendors;
        this.vendorOptions = vendors.map(v => ({
          value: v.id,
          label: v.name,
          sublabel: v.city || ''
        }));
        this.vehicleOptions = vehicles;
        this.vehicleSelectOptions = vehicles.map(vehicle => ({
          value: vehicle.vehicleNumber,
          label: vehicle.vehicleNumber,
          sublabel: vehicle.name
        }));

        this.categoryOptions = categories.map(c => ({ value: c.id, label: c.name }));

        this.purchaseReturnService.getLatestRates(this.products.map(p => p.id))
          .pipe(finalize(() => this.loadPurchaseReturn()))
          .subscribe({
            next: rates => {
              this.latestRatesByProductId = new Map(rates.map(r => [r.productId, r.rate]));
            },
            error: () => {
              this.latestRatesByProductId.clear();
            }
          });
      },
      error: () => {
        this.toast.error('Unable to load form data.');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  loadPurchaseReturn(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.purchaseReturnId = +idParam;
      this.isEditMode = true;
      this.purchaseReturnService.getById(this.purchaseReturnId).subscribe({
        next: (pr: any) => {
          this.prNumber = pr.prNumber;
          this.purchaseReturnDate = parseLocalDate(pr.date);
          this.selectedVendorId = pr.vendorId;
          this.vehicleNumber = pr.vehicleNumber || '';
          this.description = pr.description || '';
          this.lines = pr.lines.map((l: any) => {
            const product = l.productId ? this.products.find(p => p.id === l.productId) : null;
            return {
              id: l.id,
              product: l.productId && product ? {
                id: l.productId,
                name: l.productName || '',
                packing: l.packing || '',
                packingWeightKg: l.packingWeightKg
              } : null,
              qty: l.qty,
              totalWeightKg: l.totalWeightKg || 0,
              rate: l.rate,
              sortOrder: l.sortOrder
            };
          });
          this.lineCategoryIds = this.lines.map(l => {
            if (!l.product) return null;
            const product = this.products.find(p => p.id === l.product!.id);
            return product?.categoryId ?? null;
          });
          this.isReadOnlyRatedVoucher = !!pr.ratesAdded;
          this.loading = false;
          this.cdr.detectChanges();
        },
        error: () => {
          this.toast.error('Unable to load purchase return.');
          this.loading = false;
          this.cdr.detectChanges();
        }
      });
    } else {
      this.purchaseReturnService.getNextNumber().subscribe({
        next: num => {
          this.prNumber = num;
          this.addLine();
          this.loading = false;
          this.cdr.detectChanges();
        },
        error: () => {
          this.toast.error('Unable to get next purchase return number.');
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
    this.lines.push({ product: null, qty: 0, totalWeightKg: 0, rate: 0 });
    this.lineCategoryIds.push(null);
  }

  removeLine(index: number): void {
    if (this.lines.length > 1) {
      this.lines.splice(index, 1);
      this.lineCategoryIds.splice(index, 1);
    }
  }

  onCategoryChange(lineIndex: number, categoryId: number): void {
    this.lineCategoryIds[lineIndex] = categoryId;
    this.lines[lineIndex].product = null;
  }

  getFilteredProductOptions(lineIndex: number): SelectOption[] {
    const catId = this.lineCategoryIds[lineIndex];
    if (catId == null) return [];
    return this.productOptions.filter(opt => {
      const product = this.products.find(p => p.id === opt.value);
      return product?.categoryId === catId;
    });
  }

  onProductChange(line: PurchaseReturnLine, productId: number): void {
    const acct = this.products.find(p => p.id === productId);
    if (acct) {
      line.product = {
        id: acct.id,
        name: acct.name,
        packing: acct.packing!,
        packingWeightKg: acct.packingWeightKg!
      };
      if (!line.rate || line.rate <= 0) {
        line.rate = this.latestRatesByProductId.get(acct.id) ?? 0;
      }
    } else {
      line.product = null;
    }
  }

  getLineTotalWeight(line: PurchaseReturnLine): number {
    return line.totalWeightKg || 0;
  }

  getLineAmount(line: PurchaseReturnLine): number {
    return getPurchaseLineAmount({ totalWeightKg: line.totalWeightKg, rate: line.rate });
  }

  get totalBags(): number {
    return getPurchaseTotalBags(this.lines.map(l => ({ qty: l.qty })));
  }

  get totalWeight(): number {
    return getPurchaseTotalWeight(this.lines.map(l => ({ totalWeightKg: l.totalWeightKg })));
  }

  get totalAmount(): number {
    return getPurchaseTotalAmount(this.lines.map(l => ({ totalWeightKg: l.totalWeightKg, rate: l.rate })));
  }

  get formattedDate(): string {
    return formatDateDisplay(this.purchaseReturnDate);
  }

  private buildRequest(): any {
    return {
      date: toDateInputValue(this.purchaseReturnDate),
      vendorId: this.selectedVendorId!,
      vehicleNumber: this.vehicleNumber || null,
      description: this.description,
      lines: this.lines
        .filter(l => l.product)
        .map((l, i) => ({
          productId: l.product!.id,
          qty: l.qty,
          totalWeightKg: l.totalWeightKg,
          rate: l.rate,
          sortOrder: i
        }))
    };
  }

  get validLines(): any[] {
    return this.lines.filter(l => l.product && l.qty > 0 && l.totalWeightKg > 0);
  }

  get canSave(): boolean {
    return !this.isReadOnlyRatedVoucher && !!this.selectedVendorId && this.validLines.length > 0;
  }

  private validate(): boolean {
    if (this.isReadOnlyRatedVoucher && !this.isAdmin) {
      this.toast.error('Only admins can edit a rated purchase return.');
      return false;
    }
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
    if (linesWithProduct.some(l => !l.totalWeightKg || l.totalWeightKg <= 0)) {
      this.toast.error('All line items must have total kg greater than zero.');
      return false;
    }
    return true;
  }

  async discard(): Promise<void> {
    const confirmed = await this.confirmModal.confirm({
      title: 'Discard Purchase Return',
      message: 'Are you sure you want to discard this purchase return? All unsaved changes will be lost.',
      confirmText: 'Discard',
      cancelText: 'Cancel'
    });
    if (confirmed) this.resetForm();
  }

  save(): void {
    this.persistVoucher(false);
  }

  saveAndPrint(): void {
    this.persistVoucher(true);
  }

  private persistVoucher(openPdf: boolean): void {
    if (!this.validate()) return;

    const req = this.buildRequest();
    const isEdit = this.isEditMode && this.purchaseReturnId !== null;
    const request$ = isEdit
      ? this.purchaseReturnService.update(this.purchaseReturnId!, req)
      : this.purchaseReturnService.create(req);
    const fallbackMessage = isEdit
      ? 'Unable to save purchase return.'
      : 'Unable to create purchase return.';

    request$.subscribe({
      next: purchaseReturn => {
        if (isEdit) {
          this.toast.success('Purchase return updated successfully.');
          if (openPdf) {
            this.purchaseReturnService.openPdfInNewTab(purchaseReturn.id);
          }
          this.router.navigate(['/pending']);
          return;
        }

        this.toast.success(`${purchaseReturn.prNumber} created successfully.`);
        if (openPdf) {
          this.purchaseReturnService.openPdfInNewTab(purchaseReturn.id);
        }
        this.resetForm();
      },
      error: err => {
        this.toast.error(getApiErrorMessage(err, fallbackMessage));
      }
    });
  }

  private resetForm(): void {
    this.selectedVendorId = null;
    this.vehicleNumber = '';
    this.description = '';
    this.purchaseReturnDate = new Date();
    this.isReadOnlyRatedVoucher = false;
    this.lines = [];
    this.addLine();
    this.purchaseReturnService.getNextNumber().subscribe({
      next: num => {
        this.prNumber = num;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to get next purchase return number.');
      }
    });
  }
}

interface PurchaseReturnLine {
  id?: number;
  product: { id: number; name: string; packing: string; packingWeightKg: number } | null;
  qty: number;
  totalWeightKg: number;
  rate: number;
  sortOrder?: number;
}
