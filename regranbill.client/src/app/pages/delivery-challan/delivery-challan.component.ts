import { Component, OnInit, HostListener, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { catchError, finalize, forkJoin, of } from 'rxjs';
import { AccountService } from '../../services/account.service';
import { AuthService } from '../../services/auth.service';
import { DeliveryChallanService } from '../../services/delivery-challan.service';
import { CompanySettingsService } from '../../services/company-settings.service';
import { CategoryService } from '../../services/category.service';
import { ToastService } from '../../services/toast.service';
import { ConfirmModalService } from '../../services/confirm-modal.service';
import { Account, AccountType, PartyRole } from '../../models/account.model';
import { Category } from '../../models/category.model';
import { ProductLine, Cartage } from '../../models/delivery-challan.model';
import { SelectOption } from '../../components/searchable-select/searchable-select.component';
import { VehicleOption } from '../../models/company-settings.model';
import { formatDateDisplay, toDateInputValue } from '../../utils/date-utils';
import { getDeliveryLineWeight, getDeliveryLineAmount, getDeliveryTotalBags, getDeliveryTotalWeight, getDeliveryTotalAmount, isPackedLine } from '../../utils/delivery-calculations';
import { getApiErrorMessage } from '../../utils/api-error';

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

  description = '';

  products: Account[] = [];
  customers: Account[] = [];
  transporters: Account[] = [];
  vehicleOptions: VehicleOption[] = [];
  lines: ProductLine[] = [];
  loading = true;
  private latestRatesByProductId = new Map<number, number>();

  // Dropdown options
  customerOptions: SelectOption[] = [];
  productOptions: SelectOption[] = [];
  transporterOptions: SelectOption[] = [];
  categoryOptions: SelectOption[] = [];
  vehicleSelectOptions: SelectOption[] = [];
  rbpOptions: SelectOption[] = [
    { value: 'Yes', label: 'Yes' },
    { value: 'No', label: 'No' }
  ];

  // Category filter per line
  lineCategoryIds: (number | null)[] = [];

  // Cartage
  cartage: Cartage | null = null;
  showCartageForm = false;
  cartageTransporterId: number | null = null;
  cartageAmount: number = 0;
  isReadOnlyRatedVoucher = false;

  // Add account modal
  showAddAccountModal = false;
  addAccountPrefillName = '';
  addAccountDefaultType?: AccountType;
  addAccountDefaultRole?: PartyRole;
  addAccountDefaultCategoryId?: number;
  private addProductTargetLineIndex = -1;

  constructor(
    private accountService: AccountService,
    private authService: AuthService,
    private dcService: DeliveryChallanService,
    private companySettingsService: CompanySettingsService,
    private categoryService: CategoryService,
    private route: ActivatedRoute,
    private router: Router,
    private cdr: ChangeDetectorRef,
    private toast: ToastService,
    private confirmModal: ConfirmModalService
  ) {}

  get canSeeRates(): boolean {
    return this.authService.hasPage('voucher-rates');
  }

  ngOnInit(): void {
    this.loadData();
  }

  onAddCustomerClicked(prefillName: string): void {
    this.addAccountPrefillName = prefillName;
    this.addAccountDefaultType = AccountType.Party;
    this.addAccountDefaultRole = PartyRole.Customer;
    this.addAccountDefaultCategoryId = undefined;
    this.addProductTargetLineIndex = -1;
    this.showAddAccountModal = true;
  }

  onAddProductClicked(lineIndex: number, prefillName: string): void {
    this.addAccountPrefillName = prefillName;
    this.addAccountDefaultType = AccountType.Product;
    this.addAccountDefaultRole = undefined;
    this.addAccountDefaultCategoryId = this.lineCategoryIds[lineIndex] ?? undefined;
    this.addProductTargetLineIndex = lineIndex;
    this.showAddAccountModal = true;
  }

  onAddAccountModalClosed(): void {
    this.showAddAccountModal = false;
    this.addProductTargetLineIndex = -1;
  }

  onNewAccountCreated(account: Account): void {
    this.showAddAccountModal = false;

    if (this.addProductTargetLineIndex >= 0) {
      this.products = [...this.products, account];
      this.productOptions = this.products.map(p => ({
        value: p.id,
        label: p.name,
        sublabel: p.packing || ''
      }));
      this.lineCategoryIds[this.addProductTargetLineIndex] = account.categoryId;
      this.onProductChange(this.lines[this.addProductTargetLineIndex], account.id);
      this.addProductTargetLineIndex = -1;
    } else {
      this.customers = [...this.customers, account];
      this.customerOptions = this.customers.map(c => ({
        value: c.id,
        label: c.name,
        sublabel: c.city || ''
      }));
      this.selectedCustomerId = account.id;
    }

    this.cdr.detectChanges();
  }

  loadData(): void {
    this.loading = true;
    forkJoin({
      products: this.accountService.getProducts(),
      customers: this.accountService.getCustomers(),
      transporters: this.accountService.getTransporters(),
      vehicles: this.companySettingsService.getVehicles().pipe(catchError(() => of([]))),
      categories: this.categoryService.getAll()
    }).subscribe({
      next: ({ products, customers, transporters, vehicles, categories }) => {
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

        this.vehicleOptions = vehicles;
        this.categoryOptions = categories.map(c => ({ value: c.id, label: c.name }));
        this.vehicleSelectOptions = vehicles.map(vehicle => ({
          value: vehicle.vehicleNumber,
          label: vehicle.vehicleNumber,
          sublabel: vehicle.name
        }));

        this.dcService.getLatestRates(this.products.map(p => p.id))
          .pipe(finalize(() => this.loadChallan()))
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
            this.lines = dc.lines.map((l: any) => {
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
            this.cartage = dc.cartage ? {
              transporterId: dc.cartage.transporterId,
              transporterName: dc.cartage.transporterName || '',
              city: dc.cartage.city || '',
                amount: dc.cartage.amount
              } : null;
            this.isReadOnlyRatedVoucher = !!dc.ratesAdded;
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

  onProductChange(line: ProductLine, productId: number): void {
    const acct = this.products.find(p => p.id === productId);
    if (acct) {
      line.product = {
        id: acct.id,
        name: acct.name,
        packing: acct.packing!,
        packingWeightKg: acct.packingWeightKg!
      };

      if (this.canSeeRates && (!line.rate || line.rate <= 0)) {
        line.rate = this.latestRatesByProductId.get(acct.id) ?? 0;
      }
    } else {
      line.product = null;
    }
  }

  onRbpChange(line: ProductLine, val: string): void {
    line.rbp = val as 'Yes' | 'No';
  }

  getLineTotalWeight(line: ProductLine): number {
    return getDeliveryLineWeight({ qty: line.qty, rbp: line.rbp, packingWeightKg: line.product?.packingWeightKg });
  }

  isLoose(line: ProductLine): boolean {
    return !isPackedLine(line.rbp);
  }

  getLineAmount(line: ProductLine): number {
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
    return formatDateDisplay(this.dcDate);
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
      date: toDateInputValue(this.dcDate),
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
    return !this.isReadOnlyRatedVoucher && !!this.selectedCustomerId && this.validLines.length > 0;
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
      title: 'Discard Challan',
      message: 'Are you sure you want to discard this delivery challan? All unsaved changes will be lost.',
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
    const isEdit = this.isEditMode && this.challanId !== null;
    const request$ = isEdit
      ? this.dcService.update(this.challanId!, req)
      : this.dcService.create(req);
    const fallbackMessage = isEdit
      ? 'Unable to save delivery challan.'
      : 'Unable to create delivery challan.';

    request$.subscribe({
      next: dc => {
        if (isEdit) {
          this.toast.success('Challan updated successfully.');
          if (openPdf) {
            this.dcService.openPdfInNewTab(dc.id);
          }
          this.router.navigate(['/pending']);
          return;
        }

        this.toast.success(`${dc.dcNumber} created successfully.`);
        if (openPdf) {
          this.dcService.openPdfInNewTab(dc.id);
        }
        this.resetForm();
      },
      error: err => {
        this.toast.error(getApiErrorMessage(err, fallbackMessage));
      }
    });
  }

  private resetForm(): void {
    this.selectedCustomerId = null;
    this.vehicleNumber = '';
    this.description = '';
    this.dcDate = new Date();
    this.cartage = null;
    this.isReadOnlyRatedVoucher = false;
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
