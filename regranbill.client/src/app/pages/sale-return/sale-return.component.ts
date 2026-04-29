import { Component, OnInit, HostListener, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { catchError, finalize, forkJoin, of } from 'rxjs';
import { AccountService } from '../../services/account.service';
import { AuthService } from '../../services/auth.service';
import { SaleReturnService } from '../../services/sale-return.service';
import { CompanySettingsService } from '../../services/company-settings.service';
import { CategoryService } from '../../services/category.service';
import { ToastService } from '../../services/toast.service';
import { ConfirmModalService } from '../../services/confirm-modal.service';
import { Account } from '../../models/account.model';
import { Category } from '../../models/category.model';
import { SaleReturnLine, SaleReturnViewModel, SaleReturnUpsertRequest } from '../../models/sale-return.model';
import { SelectOption } from '../../components/searchable-select/searchable-select.component';
import { VehicleOption } from '../../models/company-settings.model';
import { formatDateDisplay, parseLocalDate, toDateInputValue } from '../../utils/date-utils';
import { getDeliveryLineWeight, getDeliveryLineAmount, getDeliveryTotalBags, getDeliveryTotalWeight, getDeliveryTotalAmount, isPackedLine } from '../../utils/delivery-calculations';

@Component({
  selector: 'app-sale-return',
  templateUrl: './sale-return.component.html',
  styleUrl: './sale-return.component.css',
  standalone: false
})
export class SaleReturnComponent implements OnInit {
  saleReturnId: number | null = null;
  isEditMode = false;
  srNumber = '';
  saleReturnDate = new Date();

  get saleReturnDateIso(): string {
    return toDateInputValue(this.saleReturnDate);
  }

  set saleReturnDateIso(val: string) {
    if (!val) return;
    this.saleReturnDate = parseLocalDate(val);
  }
  selectedCustomerId: number | null = null;
  vehicleNumber = '';
  description = '';

  products: Account[] = [];
  customers: Account[] = [];
  vehicleOptions: VehicleOption[] = [];
  lines: SaleReturnLine[] = [];
  loading = true;
  private latestRatesByProductId = new Map<number, number>();

  customerOptions: SelectOption[] = [];
  productOptions: SelectOption[] = [];
  categoryOptions: SelectOption[] = [];
  rbpOptions: SelectOption[] = [
    { value: 'Yes', label: 'Yes' },
    { value: 'No', label: 'No' }
  ];
  vehicleSelectOptions: SelectOption[] = [];

  // Category filter per line
  lineCategoryIds: (number | null)[] = [];

  constructor(
    private accountService: AccountService,
    private authService: AuthService,
    private saleReturnService: SaleReturnService,
    private companySettingsService: CompanySettingsService,
    private categoryService: CategoryService,
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
      customers: this.accountService.getCustomers(),
      vehicles: this.companySettingsService.getVehicles().pipe(catchError(() => of([]))),
      categories: this.categoryService.getAll()
    }).subscribe({
      next: ({ products, customers, vehicles, categories }) => {
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

        this.vehicleOptions = vehicles;
        this.categoryOptions = categories.map(c => ({ value: c.id, label: c.name }));
        this.vehicleSelectOptions = vehicles.map(vehicle => ({
          value: vehicle.vehicleNumber,
          label: vehicle.vehicleNumber,
          sublabel: vehicle.name
        }));

        this.saleReturnService.getLatestRates(this.products.map(p => p.id))
          .pipe(finalize(() => this.loadSaleReturn()))
          .subscribe({
            next: rates => {
              this.latestRatesByProductId = new Map(rates.map(rate => [rate.productId, rate.rate]));
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

  loadSaleReturn(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.saleReturnId = +idParam;
      this.isEditMode = true;
      this.saleReturnService.getById(this.saleReturnId).subscribe({
        next: (sr: SaleReturnViewModel) => {
          this.srNumber = sr.srNumber;
          this.saleReturnDate = parseLocalDate(sr.date);
          this.selectedCustomerId = sr.customerId;
          this.vehicleNumber = sr.vehicleNumber || '';
          this.description = sr.description || '';
          this.lines = sr.lines.map((l: any) => {
            const product = l.productId ? this.products.find(p => p.id === l.productId) : null;
            return {
              id: l.id,
              product: l.productId && product ? {
                id: l.productId,
                name: l.productName || '',
                packing: l.packing || '',
                packingWeightKg: l.packingWeightKg
              } : null,
              rbp: l.rbp as 'Yes' | 'No',
              qty: l.qty,
              rate: l.rate,
              sortOrder: l.sortOrder
            };
          });
          this.lineCategoryIds = this.lines.map(l => {
            if (!l.product) return null;
            const product = this.products.find(p => p.id === l.product!.id);
            return product?.categoryId ?? null;
          });
          this.loading = false;
          this.cdr.detectChanges();
        },
        error: () => {
          this.toast.error('Unable to load sale return.');
          this.loading = false;
          this.cdr.detectChanges();
        }
      });
    } else {
      this.saleReturnService.getNextNumber().subscribe({
        next: num => {
          this.srNumber = num;
          this.addLine();
          this.loading = false;
          this.cdr.detectChanges();
        },
        error: () => {
          this.toast.error('Unable to get next sale return number.');
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

  onProductChange(line: SaleReturnLine, productId: number): void {
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

  onRbpChange(line: SaleReturnLine, val: string): void {
    line.rbp = val as 'Yes' | 'No';
  }

  getLineTotalWeight(line: SaleReturnLine): number {
    return getDeliveryLineWeight({ qty: line.qty, rbp: line.rbp, packingWeightKg: line.product?.packingWeightKg });
  }

  isLoose(line: SaleReturnLine): boolean {
    return !isPackedLine(line.rbp);
  }

  getLineAmount(line: SaleReturnLine): number {
    return getDeliveryLineAmount({ qty: line.qty, rate: line.rate, rbp: line.rbp, packingWeightKg: line.product?.packingWeightKg });
  }

  get totalBags(): number {
    return getDeliveryTotalBags(this.lines.map(l => ({ qty: l.qty, rbp: l.rbp, packingWeightKg: l.product?.packingWeightKg })));
  }

  get totalWeight(): number {
    return getDeliveryTotalWeight(this.lines.map(l => ({ qty: l.qty, rbp: l.rbp, packingWeightKg: l.product?.packingWeightKg })));
  }

  get totalAmount(): number {
    return getDeliveryTotalAmount(this.lines.map(l => ({ qty: l.qty, rate: l.rate, rbp: l.rbp, packingWeightKg: l.product?.packingWeightKg })));
  }

  get formattedDate(): string {
    return formatDateDisplay(this.saleReturnDate);
  }

  private buildRequest(): SaleReturnUpsertRequest {
    return {
      date: this.saleReturnDate,
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
        }))
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

  async discard(): Promise<void> {
    const confirmed = await this.confirmModal.confirm({
      title: 'Discard Sale Return',
      message: 'Are you sure you want to discard this sale return? All unsaved changes will be lost.',
      confirmText: 'Discard',
      cancelText: 'Cancel'
    });
    if (confirmed) this.resetForm();
  }

  save(): void {
    if (!this.validate()) return;
    const req = this.buildRequest();

    if (this.isEditMode && this.saleReturnId) {
      this.saleReturnService.update(this.saleReturnId, req).subscribe({
        next: () => {
          this.toast.success('Sale return updated successfully.');
          this.router.navigate(['/pending']);
        },
        error: err => {
          this.toast.error(err?.error?.message || 'Unable to save sale return.');
        }
      });
    } else {
      this.saleReturnService.create(req).subscribe({
        next: sr => {
          this.toast.success(`${sr.srNumber} created successfully.`);
          this.resetForm();
        },
        error: err => {
          this.toast.error(err?.error?.message || 'Unable to create sale return.');
        }
      });
    }
  }

  saveAndPrint(): void {
    if (!this.validate()) return;
    const req = this.buildRequest();

    if (this.isEditMode && this.saleReturnId) {
      this.saleReturnService.update(this.saleReturnId, req).subscribe({
        next: sr => {
          this.toast.success('Sale return updated successfully.');
          this.saleReturnService.openPdfInNewTab(sr.id);
          this.router.navigate(['/pending']);
        },
        error: err => {
          this.toast.error(err?.error?.message || 'Unable to save sale return.');
        }
      });
    } else {
      this.saleReturnService.create(req).subscribe({
        next: sr => {
          this.toast.success(`${sr.srNumber} created successfully.`);
          this.saleReturnService.openPdfInNewTab(sr.id);
          this.resetForm();
        },
        error: err => {
          this.toast.error(err?.error?.message || 'Unable to create sale return.');
        }
      });
    }
  }

  private resetForm(): void {
    this.selectedCustomerId = null;
    this.vehicleNumber = '';
    this.description = '';
    this.saleReturnDate = new Date();
    this.lines = [];
    this.addLine();
    this.saleReturnService.getNextNumber().subscribe({
      next: num => {
        this.srNumber = num;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to get next sale return number.');
      }
    });
  }
}
