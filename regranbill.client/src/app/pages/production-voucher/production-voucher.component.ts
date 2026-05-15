import { ChangeDetectorRef, Component, HostListener, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { AccountService } from '../../services/account.service';
import { CategoryService } from '../../services/category.service';
import { ProductionVoucherService } from '../../services/production-voucher.service';
import { DownstreamUsage, DownstreamUsageService } from '../../services/downstream-usage.service';
import { FormulationService } from '../../services/formulation.service';
import { ToastService } from '../../services/toast.service';
import { ConfirmModalService } from '../../services/confirm-modal.service';
import { InventoryLotService } from '../../services/inventory-lot.service';
import { Account, AccountType } from '../../models/account.model';
import { Category } from '../../models/category.model';
import {
  ProductionLineRequest,
  ProductionShortageRequest,
  ProductionVoucherUpsertRequest,
  ProductionVoucherApiDto
} from '../../models/production-voucher.model';
import { AvailableInventoryLot } from '../../models/inventory-lot.model';
import { ApplyFormulationResponse, FormulationDto } from '../../models/formulation.model';
import { SelectOption } from '../../components/searchable-select/searchable-select.component';
import { parseLocalDate, toDateInputValue } from '../../utils/date-utils';
import { getApiErrorMessage } from '../../utils/api-error';

interface Row {
  categoryId?: number | null;
  accountId: number | null;
  selectedLotId?: number | null;
  qty: number;
  weightKg: number;
  description?: string | null;
  rate?: number | null;
  rateSource?: string | null;
  lotOptions?: SelectOption[];
  availableLots?: AvailableInventoryLot[];
}

@Component({
  selector: 'app-production-voucher',
  templateUrl: './production-voucher.component.html',
  styleUrl: './production-voucher.component.css',
  standalone: false
})
export class ProductionVoucherComponent implements OnInit {
  private static readonly TOLERANCE = 0.01;
  readonly AccountType = AccountType;

  voucherId: number | null = null;
  isEditMode = false;
  voucherNumber = '';
  voucherDate = new Date();
  lotNumber = '';
  description = '';

  inputs: Row[] = [];
  outputs: Row[] = [];
  byproducts: Row[] = [];
  shortageAccountId: number | null = null;
  shortageWeightKg = 0;
  shortageUserEdited = false;

  categoryOptions: SelectOption[] = [];
  inputAccountOptions: SelectOption[] = [];
  outputAccountOptions: SelectOption[] = [];
  byproductAccountOptions: SelectOption[] = [];
  expenseAccountOptions: SelectOption[] = [];
  outputCategoryOptions: SelectOption[] = [];
  byproductCategoryOptions: SelectOption[] = [];
  shortageCategoryOptions: SelectOption[] = [];
  inputCategoryOptions: SelectOption[] = [];

  accountsById = new Map<number, Account>();
  categories: Category[] = [];
  shortageCategoryId: number | null = null;

  formulations: FormulationDto[] = [];
  formulationOptions: SelectOption[] = [];
  selectedFormulationId: number | null = null;
  formulationBatchKg = 0;

  loading = true;
  saving = false;
  downstreamUsages: DownstreamUsage[] = [];

  focusedSection: 'input' | 'output' | 'byproduct' = 'input';

  constructor(
    private accountService: AccountService,
    private categoryService: CategoryService,
    private productionService: ProductionVoucherService,
    private downstreamService: DownstreamUsageService,
    private formulationService: FormulationService,
    private inventoryLotService: InventoryLotService,
    private route: ActivatedRoute,
    private router: Router,
    private toast: ToastService,
    private confirmModal: ConfirmModalService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadData();
  }

  private loadData(): void {
    this.loading = true;
    forkJoin({
      accounts: this.accountService.getAll(),
      categories: this.categoryService.getAll(),
      formulations: this.formulationService.getAll().pipe(catchError(() => of([]))),
      inputCats: this.categoryService.getFiltered([AccountType.RawMaterial, AccountType.Product]),
      outputCats: this.categoryService.getFiltered([AccountType.Product]),
      byproductCats: this.categoryService.getFiltered([AccountType.RawMaterial, AccountType.Product]),
      shortageCats: this.categoryService.getFiltered([AccountType.Expense])
    }).subscribe({
      next: ({ accounts, categories, formulations, inputCats, outputCats, byproductCats, shortageCats }) => {
        this.accountsById = new Map(accounts.map(account => [account.id, account]));
        this.categories = categories;
        this.categoryOptions = categories.map(category => ({ value: category.id, label: category.name }));
        const inputAccts = accounts.filter(
          a => a.accountType === AccountType.Product || a.accountType === AccountType.RawMaterial
        );
        const outputAccts = accounts.filter(a => a.accountType === AccountType.Product);
        const byproductAccts = accounts.filter(
          a => a.accountType === AccountType.RawMaterial || a.accountType === AccountType.Product
        );
        const expenseAccts = accounts.filter(a => a.accountType === AccountType.Expense);

        this.inputAccountOptions = inputAccts.map(a => ({
          value: a.id, label: a.name, sublabel: a.packing || ''
        }));
        this.outputAccountOptions = outputAccts.map(a => ({
          value: a.id, label: a.name, sublabel: a.packing || ''
        }));
        this.byproductAccountOptions = byproductAccts.map(a => ({
          value: a.id, label: a.name, sublabel: a.packing || ''
        }));
        this.expenseAccountOptions = expenseAccts.map(a => ({
          value: a.id, label: a.name
        }));
        this.inputCategoryOptions = inputCats.map(c => ({ value: c.id, label: c.name }));
        this.outputCategoryOptions = outputCats.map(c => ({ value: c.id, label: c.name }));
        this.byproductCategoryOptions = byproductCats.map(c => ({ value: c.id, label: c.name }));
        this.shortageCategoryOptions = shortageCats.map(c => ({ value: c.id, label: c.name }));

        this.formulations = formulations.filter(f => f.isActive);
        this.formulationOptions = this.formulations.map(f => ({
          value: f.id, label: f.name, sublabel: f.description || ''
        }));

        this.loadVoucherOrSeed();
      },
      error: () => {
        this.toast.error('Unable to load form data.');
        this.loading = false;
        this.enforceAllWeightLimits();
        this.cdr.detectChanges();
      }
    });
  }

  private loadVoucherOrSeed(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.voucherId = +idParam;
      this.isEditMode = true;
      this.productionService.getById(this.voucherId).subscribe({
        next: voucher => this.applyVoucher(voucher),
        error: () => {
          this.toast.error('Unable to load production voucher.');
          this.loading = false;
          this.cdr.detectChanges();
        }
      });
    } else {
      this.productionService.getNextNumber().subscribe({
        next: num => {
          this.voucherNumber = num;
          this.inputs = [this.emptyRow()];
          this.outputs = [this.emptyRow()];
          this.byproducts = [];
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

  private applyVoucher(voucher: ProductionVoucherApiDto): void {
    this.voucherNumber = voucher.voucherNumber;
    this.voucherDate = parseLocalDate(voucher.date);
    this.lotNumber = voucher.lotNumber || '';
    this.description = voucher.description || '';
    this.inputs = voucher.inputs.map(l => ({
      categoryId: this.accountsById.get(l.accountId)?.categoryId ?? null,
      accountId: l.accountId,
      selectedLotId: l.selectedLotId ?? null,
      qty: l.qty,
      weightKg: l.weightKg,
      description: l.description ?? '',
      rate: l.rate ?? null,
      rateSource: l.selectedLotNumber ? `${l.selectedLotNumber}${l.vendorName ? ' · ' + l.vendorName : ''}` : null,
      lotOptions: l.selectedLotId
        ? [{
            value: l.selectedLotId,
            label: `${l.selectedLotNumber || 'Lot'}${l.vendorName ? ' · ' + l.vendorName : ''}`,
            sublabel: l.rate ? `${l.rate.toFixed(2)}/kg` : ''
          }]
        : [],
      availableLots: []
    }));
    this.outputs = voucher.outputs.map(l => ({
      categoryId: this.accountsById.get(l.accountId)?.categoryId ?? null,
      accountId: l.accountId,
      qty: l.qty,
      weightKg: l.weightKg
    }));
    this.byproducts = voucher.byproducts.map(l => ({
      categoryId: this.accountsById.get(l.accountId)?.categoryId ?? null,
      accountId: l.accountId,
      qty: l.qty,
      weightKg: l.weightKg
    }));
    if (voucher.shortage) {
      this.shortageAccountId = voucher.shortage.accountId;
      this.shortageCategoryId = this.accountsById.get(voucher.shortage.accountId)?.categoryId ?? null;
      this.shortageWeightKg = voucher.shortage.weightKg;
      this.shortageUserEdited = true;
    } else {
      this.shortageAccountId = null;
      this.shortageCategoryId = null;
      this.shortageWeightKg = 0;
      this.shortageUserEdited = false;
    }
    if (this.inputs.length === 0) this.inputs = [this.emptyRow()];
    if (this.outputs.length === 0) this.outputs = [this.emptyRow()];
    this.inputs
      .filter(row => !!row.accountId)
      .forEach(row => {
        const currentLotId = row.selectedLotId ?? null;
        this.inventoryLotService.getAvailableForProduction(row.accountId!, this.voucherId).subscribe({
          next: lots => {
            row.availableLots = lots;
            row.lotOptions = lots.map(lot => ({
              value: lot.lotId,
              label: `${lot.lotNumber} - ${lot.availableWeightKg.toFixed(2)} kg`,
              sublabel: `${lot.vendorName || 'No vendor'} - ${lot.rate.toFixed(2)}/kg`
            }));
            row.selectedLotId = currentLotId;
            this.enforceAllWeightLimits();
            this.cdr.detectChanges();
          },
          error: () => {
            row.availableLots = [];
            row.lotOptions = [];
          }
        });
      });
    this.loading = false;
    this.cdr.detectChanges();
    this.loadDownstreamUsage();
  }

  private loadDownstreamUsage(): void {
    if (!this.isEditMode || this.voucherId === null) return;
    this.downstreamService.forProduction(this.voucherId).subscribe({
      next: rows => {
        this.downstreamUsages = rows;
        this.cdr.detectChanges();
      },
      error: () => {
        this.downstreamUsages = [];
      }
    });
  }

  private emptyRow(): Row {
    return {
      categoryId: null,
      accountId: null,
      selectedLotId: null,
      qty: 0,
      weightKg: 0,
      description: '',
      rate: null,
      rateSource: null,
      lotOptions: [],
      availableLots: []
    };
  }

  onInputAccountChanged(row: Row): void {
    row.selectedLotId = null;
    row.rate = null;
    row.rateSource = null;
    row.availableLots = [];
    row.lotOptions = [];

    if (!row.accountId) {
      this.onLineChanged();
      return;
    }

    this.inventoryLotService.getAvailableForProduction(row.accountId, this.voucherId).subscribe({
      next: lots => {
        row.availableLots = lots;
        row.lotOptions = lots.map(lot => ({
          value: lot.lotId,
          label: `${lot.lotNumber} · ${lot.availableWeightKg.toFixed(2)} kg`,
          sublabel: `${lot.vendorName || 'No vendor'} · ${lot.rate.toFixed(2)}/kg`
        }));
        this.cdr.detectChanges();
      },
      error: () => {
        row.availableLots = [];
        row.lotOptions = [];
      }
    });

    this.onLineChanged();
  }

  onInputCategoryChanged(row: Row): void {
    this.onRowCategoryChanged(row);
  }

  onRowCategoryChanged(row: Row): void {
    row.accountId = null;
    row.selectedLotId = null;
    row.rate = null;
    row.rateSource = null;
    row.availableLots = [];
    row.lotOptions = [];
    this.onLineChanged();
  }

  onShortageCategoryChanged(): void {
    this.shortageAccountId = null;
  }

  getFilteredAccountOptions(accountType: AccountType, categoryId: number | null | undefined): SelectOption[] {
    if (!categoryId) {
      return [];
    }

    return [...this.accountsById.values()]
      .filter(account => account.accountType === accountType && account.categoryId === categoryId)
      .map(account => ({
        value: account.id,
        label: account.name,
        sublabel: account.packing || ''
      }));
  }

  get shortageExpenseOptions(): SelectOption[] {
    return this.getFilteredAccountOptions(AccountType.Expense, this.shortageCategoryId);
  }

  getFilteredByproductAccountOptions(categoryId: number | null | undefined): SelectOption[] {
    // Byproducts can be either RawMaterial or Product (per business rule).
    return this.getFilteredInputAccountOptions(categoryId);
  }

  getFilteredInputAccountOptions(categoryId: number | null | undefined): SelectOption[] {
    if (!categoryId) {
      return [];
    }

    return [...this.accountsById.values()]
      .filter(account =>
        account.categoryId === categoryId
        && (account.accountType === AccountType.RawMaterial || account.accountType === AccountType.Product))
      .map(account => ({
        value: account.id,
        label: account.name,
        sublabel: account.packing || ''
      }));
  }

  onInputLotChanged(row: Row): void {
    if (row.selectedLotId && this.isLotSelectedElsewhere(row, row.selectedLotId)) {
      row.selectedLotId = null;
      row.rate = null;
      row.rateSource = null;
      this.toast.error('This lot is already selected on another input row.');
      this.cdr.detectChanges();
      return;
    }

    const lot = this.getSelectedLot(row);
    if (!lot) {
      row.rate = null;
      row.rateSource = null;
      return;
    }

    row.rate = lot.rate;
    row.rateSource = `${lot.sourceVoucherNumber} · ${lot.sourceDate} · ${lot.availableWeightKg.toFixed(2)} kg available`;
    this.enforceInputRowLimit(row);
    this.onLineChanged();
    this.cdr.detectChanges();
  }

  onInputWeightChanged(row: Row): void {
    this.onLineChanged();
  }

  onOutputWeightChanged(row: Row): void {
    this.onLineChanged();
  }

  onByproductWeightChanged(row: Row): void {
    this.onLineChanged();
  }

  getSelectedLot(row: Row): AvailableInventoryLot | null {
    return row.availableLots?.find(lot => lot.lotId === row.selectedLotId) ?? null;
  }

  getFilteredLotOptions(row: Row): SelectOption[] {
    const takenLotIds = new Set(
      this.inputs
        .filter(candidate => candidate !== row && !!candidate.selectedLotId)
        .map(candidate => candidate.selectedLotId!)
    );

    return (row.lotOptions ?? []).filter(option => !takenLotIds.has(Number(option.value)));
  }

  // Section helpers
  addInput(): void { this.inputs.push(this.emptyRow()); this.focusedSection = 'input'; }
  addOutput(): void { this.outputs.push(this.emptyRow()); this.focusedSection = 'output'; }
  addByproduct(): void { this.byproducts.push(this.emptyRow()); this.focusedSection = 'byproduct'; }

  removeInput(i: number): void { this.inputs.splice(i, 1); this.recomputeShortage(); }
  removeOutput(i: number): void { this.outputs.splice(i, 1); this.recomputeShortage(); }
  removeByproduct(i: number): void { this.byproducts.splice(i, 1); this.recomputeShortage(); }

  focusSection(section: 'input' | 'output' | 'byproduct'): void { this.focusedSection = section; }

  @HostListener('document:keydown', ['$event'])
  handleKeyboardShortcut(event: KeyboardEvent): void {
    if (event.altKey && event.key === 'n') {
      event.preventDefault();
      if (this.focusedSection === 'input') this.addInput();
      else if (this.focusedSection === 'output') this.addOutput();
      else this.addByproduct();
    }
  }

  get totalInputKg(): number {
    return this.round(this.inputs.reduce((sum, l) => sum + this.numeric(l.weightKg), 0));
  }
  get totalOutputKg(): number {
    return this.round(this.outputs.reduce((sum, l) => sum + this.numeric(l.weightKg), 0));
  }
  get totalByproductKg(): number {
    return this.round(this.byproducts.reduce((sum, l) => sum + this.numeric(l.weightKg), 0));
  }
  get balanceRhs(): number {
    return this.round(this.totalOutputKg + this.totalByproductKg + this.numeric(this.shortageWeightKg));
  }
  get balanceDelta(): number {
    return this.round(this.totalInputKg - this.balanceRhs);
  }
  get isBalanced(): boolean {
    return Math.abs(this.balanceDelta) <= ProductionVoucherComponent.TOLERANCE;
  }
  get balanceState(): 'balanced' | 'near' | 'off' {
    const abs = Math.abs(this.balanceDelta);
    if (abs <= ProductionVoucherComponent.TOLERANCE) return 'balanced';
    if (abs <= 1) return 'near';
    return 'off';
  }

  onLineChanged(): void {
    this.enforceAllWeightLimits();
    if (!this.shortageUserEdited) {
      this.shortageWeightKg = this.suggestedShortage();
      this.enforceAllWeightLimits();
    }
  }

  suggestedShortage(): number {
    const suggestion = this.totalInputKg - this.totalOutputKg - this.totalByproductKg;
    return Math.max(0, this.round(suggestion));
  }

  onShortageChanged(): void {
    this.shortageUserEdited = true;
    this.enforceAllWeightLimits();
  }

  recomputeShortage(): void {
    if (!this.shortageUserEdited) {
      this.shortageWeightKg = this.suggestedShortage();
    }
  }

  resetShortageOverride(): void {
    this.shortageUserEdited = false;
    this.shortageWeightKg = this.suggestedShortage();
  }

  applyFormulation(): void {
    if (!this.selectedFormulationId) {
      this.toast.error('Pick a formulation first.');
      return;
    }
    const batch = this.numeric(this.formulationBatchKg);
    if (batch <= 0) {
      this.toast.error('Enter a total input kg greater than zero.');
      return;
    }
    this.formulationService.apply(this.selectedFormulationId, batch).subscribe({
      next: response => this.fillFromFormulation(response),
      error: err => this.toast.error(getApiErrorMessage(err, 'Unable to apply formulation.'))
    });
  }

  private fillFromFormulation(response: ApplyFormulationResponse): void {
    this.inputs = response.inputs.length
      ? response.inputs.map(l => ({
          categoryId: this.accountsById.get(l.accountId)?.categoryId ?? null,
          accountId: l.accountId,
          selectedLotId: null,
          qty: l.qty,
          weightKg: l.weightKg,
          lotOptions: [],
          availableLots: []
        }))
      : [this.emptyRow()];
    this.outputs = response.outputs.length
      ? response.outputs.map(l => ({
          categoryId: this.accountsById.get(l.accountId)?.categoryId ?? null,
          accountId: l.accountId,
          qty: l.qty,
          weightKg: l.weightKg
        }))
      : [this.emptyRow()];
    this.byproducts = response.byproducts.map(l => ({
      categoryId: this.accountsById.get(l.accountId)?.categoryId ?? null,
      accountId: l.accountId,
      qty: l.qty,
      weightKg: l.weightKg
    }));
    if (response.shortage) {
      this.shortageAccountId = response.shortage.accountId;
      this.shortageCategoryId = this.accountsById.get(response.shortage.accountId)?.categoryId ?? null;
      this.shortageWeightKg = response.shortage.weightKg;
      this.shortageUserEdited = true;
    } else {
      this.shortageCategoryId = null;
      this.shortageWeightKg = this.suggestedShortage();
      this.shortageUserEdited = false;
    }
    this.toast.success('Formulation applied. Pick lots for the input rows and save when ready.');
    this.cdr.detectChanges();
  }

  get canSave(): boolean {
    if (this.saving) return false;
    if (!this.isBalanced) return false;
    if (this.validInputs.length === 0) return false;
    if (this.validOutputs.length === 0) return false;
    if (this.numeric(this.shortageWeightKg) > 0 && !this.shortageAccountId) return false;
    if (!this.inputsHaveLotsAndRates) return false;
    if (this.hasInputOverconsumption) return false;
    return true;
  }

  get balanceErrorTooltip(): string {
    if (this.isBalanced) return '';
    const sign = this.balanceDelta > 0 ? 'over' : 'short';
    return `Mass balance off by ${Math.abs(this.balanceDelta).toFixed(2)} kg (${sign}). Adjust outputs, byproducts, or shortage to balance.`;
  }

  get validInputs(): Row[] {
    return this.inputs.filter(l => l.accountId && l.selectedLotId && this.numeric(l.weightKg) > 0);
  }

  get inputsHaveLotsAndRates(): boolean {
    return this.validInputs.every(l => !!l.selectedLotId && this.numeric(l.rate) > 0);
  }

  get hasInputOverconsumption(): boolean {
    const requiredByLot = new Map<number, number>();
    for (const row of this.validInputs) {
      const lotId = row.selectedLotId!;
      requiredByLot.set(lotId, (requiredByLot.get(lotId) ?? 0) + this.numeric(row.weightKg));
    }

    return Array.from(requiredByLot.entries()).some(([lotId, required]) => {
      const lot = this.inputs
        .flatMap(row => row.availableLots ?? [])
        .find(candidate => candidate.lotId === lotId);
      return !!lot && required > lot.availableWeightKg;
    });
  }

  get totalInputCost(): number {
    return this.round(this.validInputs.reduce((s, l) => s + this.numeric(l.weightKg) * this.numeric(l.rate), 0));
  }
  get validOutputs(): Row[] {
    return this.outputs.filter(l => l.accountId && this.numeric(l.weightKg) > 0);
  }
  get validByproducts(): Row[] {
    return this.byproducts.filter(l => l.accountId && this.numeric(l.weightKg) > 0);
  }

  save(): void {
    this.enforceAllWeightLimits();
    if (!this.canSave) {
      if (!this.isBalanced) {
        this.toast.error(this.balanceErrorTooltip);
      } else if (this.validInputs.length === 0 || this.validOutputs.length === 0) {
        this.toast.error('Add at least one input and one output.');
      } else if (this.numeric(this.shortageWeightKg) > 0 && !this.shortageAccountId) {
        this.toast.error('Pick a Production Loss account for the shortage.');
      } else if (!this.inputsHaveLotsAndRates) {
        this.toast.error('Each input line needs a selected lot and a rate > 0.');
      } else if (this.hasInputOverconsumption) {
        this.toast.error('One or more input rows exceed the selected lot availability.');
      }
      return;
    }

    const req: ProductionVoucherUpsertRequest = {
      date: toDateInputValue(this.voucherDate),
      lotNumber: this.lotNumber || null,
      description: this.description || null,
      formulationId: this.selectedFormulationId,
      inputs: this.toLines(this.validInputs, { includeDescription: true, includeLotRate: true }),
      outputs: this.toLines(this.validOutputs),
      byproducts: this.toLines(this.validByproducts),
      shortage: this.numeric(this.shortageWeightKg) > 0 && this.shortageAccountId
        ? { accountId: this.shortageAccountId, weightKg: this.numeric(this.shortageWeightKg) } as ProductionShortageRequest
        : null
    };

    this.saving = true;
    const request$ = this.isEditMode && this.voucherId
      ? this.productionService.update(this.voucherId, req)
      : this.productionService.create(req);
    const fallback = this.isEditMode ? 'Unable to update production voucher.' : 'Unable to create production voucher.';

    request$.subscribe({
      next: voucher => {
        this.saving = false;
        if (this.isEditMode) {
          this.toast.success('Production voucher updated.');
          this.router.navigate(['/pending-productions']);
        } else {
          this.toast.success(`${voucher.voucherNumber} created successfully.`);
          this.resetForm();
        }
      },
      error: err => {
        this.saving = false;
        this.toast.error(getApiErrorMessage(err, fallback));
      }
    });
  }

  private toLines(rows: Row[], opts: { includeDescription?: boolean; includeLotRate?: boolean } = {}): ProductionLineRequest[] {
    return rows.map((r, i) => ({
      accountId: r.accountId!,
      selectedLotId: opts.includeLotRate ? (r.selectedLotId ?? null) : null,
      qty: Math.max(0, Math.round(this.numeric(r.qty))),
      weightKg: this.round(this.numeric(r.weightKg)),
      description: opts.includeDescription ? (r.description?.trim() || null) : null,
      sortOrder: i,
      rate: opts.includeLotRate ? (r.rate ?? null) : null
    }));
  }

  async discard(): Promise<void> {
    const confirmed = await this.confirmModal.confirm({
      title: 'Discard Voucher',
      message: 'Discard all unsaved changes?',
      confirmText: 'Discard',
      cancelText: 'Cancel'
    });
    if (confirmed) this.resetForm();
  }

  private resetForm(): void {
    this.voucherDate = new Date();
    this.lotNumber = '';
    this.description = '';
    this.selectedFormulationId = null;
    this.formulationBatchKg = 0;
    this.inputs = [this.emptyRow()];
    this.outputs = [this.emptyRow()];
    this.byproducts = [];
    this.shortageCategoryId = null;
    this.shortageAccountId = null;
    this.shortageWeightKg = 0;
    this.shortageUserEdited = false;
    if (!this.isEditMode) {
      this.productionService.getNextNumber().subscribe({
        next: num => { this.voucherNumber = num; this.cdr.detectChanges(); }
      });
    }
  }

  private numeric(value: number | null | undefined): number {
    if (value === null || value === undefined) return 0;
    const n = typeof value === 'string' ? parseFloat(value) : value;
    return Number.isFinite(n) ? n : 0;
  }

  private round(value: number): number {
    return Math.round(value * 100) / 100;
  }

  get formattedDate(): string {
    return new Intl.DateTimeFormat('en-GB', {
      day: '2-digit', month: '2-digit', year: 'numeric'
    }).format(this.voucherDate);
  }

  inputBags(rows: Row[]): number {
    return rows.reduce((s, r) => s + this.numeric(r.qty), 0);
  }
  inputKg(rows: Row[]): number {
    return this.round(rows.reduce((s, r) => s + this.numeric(r.weightKg), 0));
  }

  getMaxInputWeight(row: Row): number {
    const lot = this.getSelectedLot(row);
    if (!lot || !row.selectedLotId) {
      return 0;
    }

    const usedByOtherRows = this.inputs
      .filter(candidate => candidate !== row && candidate.selectedLotId === row.selectedLotId)
      .reduce((sum, candidate) => sum + this.numeric(candidate.weightKg), 0);

    return Math.max(0, this.round(lot.availableWeightKg - usedByOtherRows));
  }

  private isLotSelectedElsewhere(row: Row, lotId: number): boolean {
    return this.inputs.some(candidate => candidate !== row && candidate.selectedLotId === lotId);
  }

  getMaxOutputWeight(row: Row): number {
    const otherOutputs = this.outputs
      .filter(candidate => candidate !== row)
      .reduce((sum, candidate) => sum + this.numeric(candidate.weightKg), 0);
    const byproducts = this.byproducts.reduce((sum, candidate) => sum + this.numeric(candidate.weightKg), 0);
    const shortage = this.numeric(this.shortageWeightKg);
    return Math.max(0, this.round(this.totalInputKg - otherOutputs - byproducts - shortage));
  }

  getMaxByproductWeight(row: Row): number {
    const outputs = this.outputs.reduce((sum, candidate) => sum + this.numeric(candidate.weightKg), 0);
    const otherByproducts = this.byproducts
      .filter(candidate => candidate !== row)
      .reduce((sum, candidate) => sum + this.numeric(candidate.weightKg), 0);
    const shortage = this.numeric(this.shortageWeightKg);
    return Math.max(0, this.round(this.totalInputKg - outputs - otherByproducts - shortage));
  }

  getMaxShortageWeight(): number {
    return Math.max(0, this.round(this.totalInputKg - this.totalOutputKg - this.totalByproductKg));
  }

  private enforceInputRowLimit(row: Row): void {
    const max = this.getMaxInputWeight(row);
    if (this.numeric(row.weightKg) > max) {
      row.weightKg = max;
    }
  }

  private enforceOutputRowLimit(row: Row): void {
    const max = this.getMaxOutputWeight(row);
    if (this.numeric(row.weightKg) > max) {
      row.weightKg = max;
    }
  }

  private enforceByproductRowLimit(row: Row): void {
    const max = this.getMaxByproductWeight(row);
    if (this.numeric(row.weightKg) > max) {
      row.weightKg = max;
    }
  }

  private enforceAllWeightLimits(): void {
    const seenLotIds = new Set<number>();
    for (const row of this.inputs) {
      if (!row.selectedLotId) {
        continue;
      }

      if (seenLotIds.has(row.selectedLotId)) {
        row.selectedLotId = null;
        row.rate = null;
        row.rateSource = null;
        row.weightKg = 0;
        row.qty = 0;
        continue;
      }

      seenLotIds.add(row.selectedLotId);
    }

    for (const row of this.inputs) {
      this.enforceInputRowLimit(row);
    }

    for (const row of this.outputs) {
      this.enforceOutputRowLimit(row);
    }

    for (const row of this.byproducts) {
      this.enforceByproductRowLimit(row);
    }

    const maxShortage = this.getMaxShortageWeight();
    if (this.numeric(this.shortageWeightKg) > maxShortage) {
      this.shortageWeightKg = maxShortage;
    }
  }
}
