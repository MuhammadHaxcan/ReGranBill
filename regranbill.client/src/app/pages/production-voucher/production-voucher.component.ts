import { ChangeDetectorRef, Component, HostListener, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { AccountService } from '../../services/account.service';
import { ProductionVoucherService } from '../../services/production-voucher.service';
import { FormulationService } from '../../services/formulation.service';
import { ToastService } from '../../services/toast.service';
import { ConfirmModalService } from '../../services/confirm-modal.service';
import { AccountType } from '../../models/account.model';
import {
  ProductionLineRequest,
  ProductionShortageRequest,
  ProductionVoucherUpsertRequest,
  ProductionVoucherApiDto
} from '../../models/production-voucher.model';
import { ApplyFormulationResponse, FormulationDto } from '../../models/formulation.model';
import { SelectOption } from '../../components/searchable-select/searchable-select.component';
import { parseLocalDate, toDateInputValue } from '../../utils/date-utils';
import { getApiErrorMessage } from '../../utils/api-error';

interface Row {
  accountId: number | null;
  qty: number;
  weightKg: number;
  description?: string | null;
  vendorId?: number | null;
  rate?: number | null;
  rateSource?: string | null;
}

@Component({
  selector: 'app-production-voucher',
  templateUrl: './production-voucher.component.html',
  styleUrl: './production-voucher.component.css',
  standalone: false
})
export class ProductionVoucherComponent implements OnInit {
  private static readonly TOLERANCE = 0.01;

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

  inputAccountOptions: SelectOption[] = [];
  outputAccountOptions: SelectOption[] = [];
  expenseAccountOptions: SelectOption[] = [];
  vendorOptions: SelectOption[] = [];

  formulations: FormulationDto[] = [];
  formulationOptions: SelectOption[] = [];
  selectedFormulationId: number | null = null;
  formulationBatchKg = 0;

  loading = true;
  saving = false;

  focusedSection: 'input' | 'output' | 'byproduct' = 'input';

  constructor(
    private accountService: AccountService,
    private productionService: ProductionVoucherService,
    private formulationService: FormulationService,
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
      formulations: this.formulationService.getAll().pipe(catchError(() => of([])))
    }).subscribe({
      next: ({ accounts, formulations }) => {
        const inputAccts = accounts.filter(
          a => a.accountType === AccountType.Product || a.accountType === AccountType.RawMaterial
        );
        const outputAccts = accounts.filter(a => a.accountType === AccountType.Product);
        const expenseAccts = accounts.filter(a => a.accountType === AccountType.Expense);
        const vendorAccts = accounts.filter(a => a.accountType === AccountType.Party);

        this.inputAccountOptions = inputAccts.map(a => ({
          value: a.id, label: a.name, sublabel: a.packing || ''
        }));
        this.outputAccountOptions = outputAccts.map(a => ({
          value: a.id, label: a.name, sublabel: a.packing || ''
        }));
        this.expenseAccountOptions = expenseAccts.map(a => ({
          value: a.id, label: a.name
        }));
        this.vendorOptions = vendorAccts.map(a => ({
          value: a.id, label: a.name
        }));

        this.formulations = formulations.filter(f => f.isActive);
        this.formulationOptions = this.formulations.map(f => ({
          value: f.id, label: f.name, sublabel: f.description || ''
        }));

        this.loadVoucherOrSeed();
      },
      error: () => {
        this.toast.error('Unable to load form data.');
        this.loading = false;
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
      accountId: l.accountId,
      qty: l.qty,
      weightKg: l.weightKg,
      description: l.description ?? '',
      vendorId: l.vendorId ?? null,
      rate: l.rate ?? null,
      rateSource: null
    }));
    this.outputs = voucher.outputs.map(l => ({ accountId: l.accountId, qty: l.qty, weightKg: l.weightKg }));
    this.byproducts = voucher.byproducts.map(l => ({ accountId: l.accountId, qty: l.qty, weightKg: l.weightKg }));
    if (voucher.shortage) {
      this.shortageAccountId = voucher.shortage.accountId;
      this.shortageWeightKg = voucher.shortage.weightKg;
      this.shortageUserEdited = true;
    } else {
      this.shortageAccountId = null;
      this.shortageWeightKg = 0;
      this.shortageUserEdited = false;
    }
    if (this.inputs.length === 0) this.inputs = [this.emptyRow()];
    if (this.outputs.length === 0) this.outputs = [this.emptyRow()];
    this.loading = false;
    this.cdr.detectChanges();
  }

  private emptyRow(): Row {
    return { accountId: null, qty: 0, weightKg: 0, description: '', vendorId: null, rate: null, rateSource: null };
  }

  onInputVendorOrAccountChanged(row: Row): void {
    if (!row.vendorId || !row.accountId) {
      row.rateSource = null;
      return;
    }
    this.productionService.getLatestPurchaseRates(row.vendorId, [row.accountId]).subscribe({
      next: rates => {
        const found = rates.find(r => r.accountId === row.accountId);
        if (found) {
          row.rate = found.rate;
          row.rateSource = `${found.sourceVoucherNumber} · ${found.sourceDate}`;
        } else {
          row.rateSource = 'No prior purchase';
        }
        this.cdr.detectChanges();
      },
      error: () => { row.rateSource = null; }
    });
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

  // Mass balance
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
    if (!this.shortageUserEdited) {
      this.shortageWeightKg = this.suggestedShortage();
    }
  }

  suggestedShortage(): number {
    const suggestion = this.totalInputKg - this.totalOutputKg - this.totalByproductKg;
    return Math.max(0, this.round(suggestion));
  }

  onShortageChanged(): void {
    this.shortageUserEdited = true;
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

  // Formulation apply
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
      ? response.inputs.map(l => ({ accountId: l.accountId, qty: l.qty, weightKg: l.weightKg }))
      : [this.emptyRow()];
    this.outputs = response.outputs.length
      ? response.outputs.map(l => ({ accountId: l.accountId, qty: l.qty, weightKg: l.weightKg }))
      : [this.emptyRow()];
    this.byproducts = response.byproducts.map(l => ({ accountId: l.accountId, qty: l.qty, weightKg: l.weightKg }));
    if (response.shortage) {
      this.shortageAccountId = response.shortage.accountId;
      this.shortageWeightKg = response.shortage.weightKg;
      this.shortageUserEdited = true;
    } else {
      this.shortageWeightKg = this.suggestedShortage();
      this.shortageUserEdited = false;
    }
    this.toast.success('Formulation applied. Review the lines and save when ready.');
    this.cdr.detectChanges();
  }

  // Save
  get canSave(): boolean {
    if (this.saving) return false;
    if (!this.isBalanced) return false;
    if (this.validInputs.length === 0) return false;
    if (this.validOutputs.length === 0) return false;
    if (this.numeric(this.shortageWeightKg) > 0 && !this.shortageAccountId) return false;
    if (!this.inputsHaveVendorAndRate) return false;
    return true;
  }

  get balanceErrorTooltip(): string {
    if (this.isBalanced) return '';
    const sign = this.balanceDelta > 0 ? 'over' : 'short';
    return `Mass balance off by ${Math.abs(this.balanceDelta).toFixed(2)} kg (${sign}). Adjust outputs, byproducts, or shortage to balance.`;
  }

  get validInputs(): Row[] {
    return this.inputs.filter(l => l.accountId && this.numeric(l.weightKg) > 0);
  }

  get inputsHaveVendorAndRate(): boolean {
    return this.validInputs.every(l => !!l.vendorId && this.numeric(l.rate) > 0);
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
    if (!this.canSave) {
      if (!this.isBalanced) {
        this.toast.error(this.balanceErrorTooltip);
      } else if (this.validInputs.length === 0 || this.validOutputs.length === 0) {
        this.toast.error('Add at least one input and one output.');
      } else if (this.numeric(this.shortageWeightKg) > 0 && !this.shortageAccountId) {
        this.toast.error('Pick a Production Loss account for the shortage.');
      } else if (!this.inputsHaveVendorAndRate) {
        this.toast.error('Each input line needs a vendor and a rate > 0.');
      }
      return;
    }

    const req: ProductionVoucherUpsertRequest = {
      date: toDateInputValue(this.voucherDate),
      lotNumber: this.lotNumber || null,
      description: this.description || null,
      formulationId: this.selectedFormulationId,
      inputs: this.toLines(this.validInputs, { includeDescription: true, includeVendorRate: true }),
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

  private toLines(rows: Row[], opts: { includeDescription?: boolean; includeVendorRate?: boolean } = {}): ProductionLineRequest[] {
    return rows.map((r, i) => ({
      accountId: r.accountId!,
      qty: Math.max(0, Math.round(this.numeric(r.qty))),
      weightKg: this.round(this.numeric(r.weightKg)),
      description: opts.includeDescription ? (r.description?.trim() || null) : null,
      sortOrder: i,
      vendorId: opts.includeVendorRate ? (r.vendorId ?? null) : null,
      rate: opts.includeVendorRate ? (r.rate ?? null) : null
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
}
