import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { AccountService } from '../../services/account.service';
import { CategoryService } from '../../services/category.service';
import { InventoryLotService } from '../../services/inventory-lot.service';
import { WashingVoucherService } from '../../services/washing-voucher.service';
import { DownstreamUsage, DownstreamUsageService } from '../../services/downstream-usage.service';
import { ToastService } from '../../services/toast.service';
import { Account, AccountType } from '../../models/account.model';
import { Category } from '../../models/category.model';
import { AvailableInventoryLot } from '../../models/inventory-lot.model';
import {
  CreateWashingVoucherOutputLineRequest,
  CreateWashingVoucherRequest,
  WashingVoucherDto
} from '../../models/washing-voucher.model';
import { SelectOption } from '../../components/searchable-select/searchable-select.component';
import { parseLocalDate, toDateInputValue } from '../../utils/date-utils';
import { getApiErrorMessage } from '../../utils/api-error';

const DEFAULT_WASTAGE_THRESHOLD_PCT = 10;

interface WashingOutputLineForm {
  categoryId: number | null;
  accountId: number | null;
  weightKg: number | null;
}

@Component({
  selector: 'app-washing-voucher',
  templateUrl: './washing-voucher.component.html',
  styleUrl: './washing-voucher.component.css',
  standalone: false
})
export class WashingVoucherComponent implements OnInit {
  voucherId: number | null = null;
  isEditMode = false;
  voucherNumber = '';
  voucherDate = new Date();
  description = '';

  sourceVendorId: number | null = null;
  sourceCategoryId: number | null = null;
  unwashedAccountId: number | null = null;
  selectedLotId: number | null = null;
  inputWeightKg = 0;
  thresholdPct = DEFAULT_WASTAGE_THRESHOLD_PCT;
  outputLines: WashingOutputLineForm[] = [this.createEmptyOutputLine()];

  categoryOptions: SelectOption[] = [];
  sourceCategoryOptions: SelectOption[] = [];
  outputCategoryOptions: SelectOption[] = [];
  vendorOptions: SelectOption[] = [];
  lotOptions: SelectOption[] = [];

  accountsById = new Map<number, Account>();
  categories: Category[] = [];
  availableLots: AvailableInventoryLot[] = [];
  rate = 0;
  rateSource: string | null = null;

  loading = true;
  saving = false;
  downstreamUsages: DownstreamUsage[] = [];

  constructor(
    private accountService: AccountService,
    private categoryService: CategoryService,
    private inventoryLotService: InventoryLotService,
    private washingService: WashingVoucherService,
    private downstreamService: DownstreamUsageService,
    private toast: ToastService,
    private route: ActivatedRoute,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loading = true;
    forkJoin({
      accounts: this.accountService.getAll(),
      categories: this.categoryService.getAll()
    }).subscribe({
      next: ({ accounts, categories }) => {
        this.accountsById = new Map(accounts.map(a => [a.id, a]));
        this.categories = categories;
        this.categoryOptions = categories.map(category => ({ value: category.id, label: category.name }));
        this.sourceCategoryOptions = this.buildCategoryOptionsForType(AccountType.UnwashedMaterial);
        this.outputCategoryOptions = this.buildCategoryOptionsForType(AccountType.RawMaterial);
        this.vendorOptions = accounts
          .filter(a => a.accountType === AccountType.Party)
          .map(a => ({ value: a.id, label: a.name }));
        this.loadVoucherOrSeed();
      },
      error: () => {
        this.toast.error('Unable to load washing voucher form data.');
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
      this.washingService.getById(this.voucherId).subscribe({
        next: voucher => this.applyVoucher(voucher),
        error: () => {
          this.toast.error('Unable to load washing voucher.');
          this.loading = false;
          this.cdr.detectChanges();
        }
      });
      return;
    }

    this.washingService.getNextNumber().subscribe({
      next: next => {
        this.voucherNumber = next;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load washing voucher form data.');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  private applyVoucher(voucher: WashingVoucherDto): void {
    this.voucherNumber = voucher.voucherNumber;
    this.voucherDate = parseLocalDate(voucher.date);
    this.description = voucher.description || '';
    this.sourceVendorId = voucher.sourceVendorId;
    this.unwashedAccountId = voucher.unwashedAccountId;
    this.sourceCategoryId = this.accountsById.get(voucher.unwashedAccountId)?.categoryId ?? null;
    this.selectedLotId = voucher.selectedLotId;
    this.inputWeightKg = voucher.inputWeightKg;
    this.thresholdPct = voucher.thresholdPct || DEFAULT_WASTAGE_THRESHOLD_PCT;
    this.rate = voucher.sourceRate;
    this.outputLines = voucher.outputLines.length
      ? voucher.outputLines.map(line => ({
          categoryId: this.accountsById.get(line.accountId)?.categoryId ?? null,
          accountId: line.accountId,
          weightKg: line.weightKg
        }))
      : [this.createEmptyOutputLine()];

    this.loadAvailableLots(false, () => {
      const selected = this.selectedLot;
      this.rateSource = selected
        ? `${selected.sourceVoucherNumber} - ${selected.sourceDate} - ${selected.availableWeightKg.toFixed(2)} kg available`
        : (voucher.selectedLotNumber || null);
      this.loading = false;
      this.cdr.detectChanges();
      this.loadDownstreamUsage();
    });
  }

  private loadDownstreamUsage(): void {
    if (!this.isEditMode || this.voucherId === null) return;
    this.downstreamService.forWashing(this.voucherId).subscribe({
      next: rows => {
        this.downstreamUsages = rows;
        this.cdr.detectChanges();
      },
      error: () => {
        this.downstreamUsages = [];
      }
    });
  }

  onSourceCategoryChanged(): void {
    this.unwashedAccountId = null;
    this.onVendorOrUnwashedChanged();
  }

  onVendorOrUnwashedChanged(): void {
    this.selectedLotId = null;
    this.availableLots = [];
    this.lotOptions = [];
    this.rate = 0;
    this.rateSource = null;
    this.loadAvailableLots();
  }

  private loadAvailableLots(resetSelectedLot = false, onLoaded?: () => void): void {
    if (!this.sourceVendorId || !this.unwashedAccountId) {
      onLoaded?.();
      return;
    }

    if (resetSelectedLot) {
      this.selectedLotId = null;
    }

    this.inventoryLotService.getAvailableForWashing(this.sourceVendorId, this.unwashedAccountId, this.voucherId).subscribe({
      next: lots => {
        this.availableLots = lots;
        this.lotOptions = lots.map(lot => ({
          value: lot.lotId,
          label: `${lot.lotNumber} - ${lot.availableWeightKg.toFixed(2)} kg`,
          sublabel: `${lot.sourceVoucherNumber} - ${lot.rate.toFixed(2)}/kg`
        }));
        if (lots.length === 0) {
          this.rateSource = 'No available lots for this vendor and material';
        }
        onLoaded?.();
        this.cdr.detectChanges();
      },
      error: () => {
        this.availableLots = [];
        this.lotOptions = [];
        this.rate = 0;
        this.rateSource = null;
        onLoaded?.();
        this.cdr.detectChanges();
      }
    });
  }

  onSelectedLotChanged(): void {
    const selectedLot = this.selectedLot;
    if (!selectedLot) {
      this.rate = 0;
      this.rateSource = null;
      return;
    }

    this.rate = selectedLot.rate;
    this.rateSource = `${selectedLot.sourceVoucherNumber} - ${selectedLot.sourceDate} - ${selectedLot.availableWeightKg.toFixed(2)} kg available`;
    if (this.inputWeightKg > selectedLot.availableWeightKg) {
      this.inputWeightKg = selectedLot.availableWeightKg;
    }
    this.cdr.detectChanges();
  }

  onInputWeightChanged(): void {
    this.enforceAllWeightLimits();
  }

  addOutputLine(): void {
    this.outputLines = [...this.outputLines, this.createEmptyOutputLine()];
  }

  removeOutputLine(index: number): void {
    if (this.outputLines.length === 1) {
      this.outputLines = [this.createEmptyOutputLine()];
      return;
    }

    this.outputLines = this.outputLines.filter((_, lineIndex) => lineIndex !== index);
  }

  get selectedLot(): AvailableInventoryLot | null {
    return this.availableLots.find(lot => lot.lotId === this.selectedLotId) ?? null;
  }

  get unwashedAccountOptions(): SelectOption[] {
    return this.getFilteredAccountOptions(AccountType.UnwashedMaterial, this.sourceCategoryId);
  }

  getOutputAccountOptions(line: WashingOutputLineForm): SelectOption[] {
    return this.getFilteredAccountOptions(AccountType.RawMaterial, line.categoryId);
  }

  onOutputCategoryChanged(line: WashingOutputLineForm, categoryId: number | null): void {
    line.categoryId = categoryId;
    line.accountId = null;
  }

  onOutputWeightChanged(line: WashingOutputLineForm): void {
    this.enforceAllWeightLimits();
  }

  get totalOutputWeightKg(): number {
    return this.round(this.normalizedOutputLines.reduce((sum, line) => sum + line.weightKg, 0));
  }

  get wastageKg(): number {
    return this.round(this.numeric(this.inputWeightKg) - this.totalOutputWeightKg);
  }

  get wastagePct(): number {
    const inp = this.numeric(this.inputWeightKg);
    if (inp <= 0) return 0;
    return this.round((this.wastageKg / inp) * 100);
  }

  get inputCost(): number {
    return this.round(this.numeric(this.inputWeightKg) * this.rate);
  }

  get effectiveThresholdPct(): number {
    const v = this.numeric(this.thresholdPct);
    return v > 0 ? v : DEFAULT_WASTAGE_THRESHOLD_PCT;
  }

  get excessWastageKg(): number {
    const limit = this.numeric(this.inputWeightKg) * (this.effectiveThresholdPct / 100);
    return Math.max(0, this.round(this.wastageKg - limit));
  }

  get excessWastageValue(): number {
    return this.round(this.excessWastageKg * this.rate);
  }

  get washedCost(): number {
    return this.round(this.inputCost - this.excessWastageValue);
  }

  get washedRate(): number {
    const out = this.totalOutputWeightKg;
    if (out <= 0) return 0;
    return this.round(this.washedCost / out);
  }

  get isWastageOverThreshold(): boolean {
    return this.wastagePct > this.effectiveThresholdPct;
  }

  get canSave(): boolean {
    if (this.saving) return false;
    if (!this.sourceVendorId) return false;
    if (!this.unwashedAccountId) return false;
    if (!this.selectedLotId) return false;
    if (this.numeric(this.inputWeightKg) <= 0) return false;
    if (this.selectedLot && this.numeric(this.inputWeightKg) > this.selectedLot.availableWeightKg) return false;
    if (this.rate <= 0) return false;
    if (this.normalizedOutputLines.length === 0) return false;
    if (this.hasInvalidOutputLines) return false;
    if (this.totalOutputWeightKg <= 0) return false;
    if (this.totalOutputWeightKg > this.numeric(this.inputWeightKg)) return false;
    const t = this.numeric(this.thresholdPct);
    if (t <= 0 || t > 100) return false;
    return true;
  }

  getOutputAccountName(accountId: number | null): string {
    if (!accountId) return '';
    return this.accountsById.get(accountId)?.name ?? '';
  }

  getOutputLineDebit(index: number): number {
    const validIndexes = this.outputLines
      .map((line, lineIndex) => ({ line, lineIndex }))
      .filter(({ line }) => !!line.accountId && this.numeric(line.weightKg) > 0)
      .map(item => item.lineIndex);
    const normalized = this.normalizedOutputLines;
    const normalizedIndex = validIndexes.indexOf(index);

    if (normalizedIndex < 0 || normalizedIndex >= normalized.length) return 0;

    const target = normalized[normalizedIndex];
    const lastIndex = normalized.length - 1;

    if (normalizedIndex === lastIndex) {
      const priorDebit = normalized
        .slice(0, lastIndex)
        .reduce((sum, line) => sum + this.round(line.weightKg * this.washedRate), 0);
      return this.round(this.washedCost - priorDebit);
    }

    return this.round(target.weightKg * this.washedRate);
  }

  getMaxInputWeight(): number {
    return this.selectedLot?.availableWeightKg ?? 0;
  }

  getMaxOutputWeight(line: WashingOutputLineForm): number {
    const otherOutputKg = this.outputLines
      .filter(candidate => candidate !== line)
      .reduce((sum, candidate) => sum + this.numeric(candidate.weightKg), 0);
    return Math.max(0, this.round(this.numeric(this.inputWeightKg) - otherOutputKg));
  }

  save(): void {
    this.enforceAllWeightLimits();
    if (!this.canSave) {
      if (!this.sourceVendorId) this.toast.error('Pick a source vendor.');
      else if (!this.unwashedAccountId) this.toast.error('Pick an unwashed material account.');
      else if (!this.selectedLotId) this.toast.error('Pick a source lot.');
      else if (this.selectedLot && this.numeric(this.inputWeightKg) > this.selectedLot.availableWeightKg) this.toast.error('Input weight exceeds lot availability.');
      else if (this.rate <= 0) this.toast.error('No usable rate found for the selected lot.');
      else if (this.normalizedOutputLines.length === 0) this.toast.error('Add at least one output mold line.');
      else this.toast.error('Check the input and output weights.');
      return;
    }

    const outputLines: CreateWashingVoucherOutputLineRequest[] = this.normalizedOutputLines.map(line => ({
      accountId: line.accountId,
      weightKg: line.weightKg
    }));

    const req: CreateWashingVoucherRequest = {
      date: toDateInputValue(this.voucherDate),
      description: this.description || null,
      sourceVendorId: this.sourceVendorId!,
      unwashedAccountId: this.unwashedAccountId!,
      selectedLotId: this.selectedLotId!,
      inputWeightKg: this.numeric(this.inputWeightKg),
      inputRate: this.rate,
      outputWeightKg: this.totalOutputWeightKg,
      outputLines,
      thresholdPct: this.effectiveThresholdPct
    };

    this.saving = true;
    const request$ = this.isEditMode && this.voucherId
      ? this.washingService.update(this.voucherId, req)
      : this.washingService.create(req);
    const fallbackMessage = this.isEditMode
      ? 'Unable to update washing voucher.'
      : 'Unable to save washing voucher.';

    request$.subscribe({
      next: voucher => {
        this.saving = false;
        this.toast.success(`${voucher.voucherNumber} ${this.isEditMode ? 'updated' : 'saved'} - washed rate ${voucher.washedRate.toFixed(2)}/kg`
          + (voucher.excessWastageKg > 0 ? `, ${voucher.excessWastageKg.toFixed(2)} kg excess charged to vendor` : ''));

        if (this.isEditMode) {
          this.router.navigate(['/rated-vouchers']);
          return;
        }

        this.resetForm();
      },
      error: err => {
        this.saving = false;
        this.toast.error(getApiErrorMessage(err, fallbackMessage));
      }
    });
  }

  private resetForm(): void {
    this.voucherId = null;
    this.isEditMode = false;
    this.voucherDate = new Date();
    this.description = '';
    this.sourceVendorId = null;
    this.sourceCategoryId = null;
    this.unwashedAccountId = null;
    this.selectedLotId = null;
    this.inputWeightKg = 0;
    this.thresholdPct = DEFAULT_WASTAGE_THRESHOLD_PCT;
    this.outputLines = [this.createEmptyOutputLine()];
    this.availableLots = [];
    this.lotOptions = [];
    this.rate = 0;
    this.rateSource = null;
    this.washingService.getNextNumber().subscribe({
      next: num => {
        this.voucherNumber = num;
        this.cdr.detectChanges();
      }
    });
  }

  private get normalizedOutputLines(): Array<{ accountId: number; weightKg: number }> {
    return this.outputLines
      .filter(line => !!line.accountId && this.numeric(line.weightKg) > 0)
      .map(line => ({
        accountId: line.accountId!,
        weightKg: this.round(this.numeric(line.weightKg))
      }));
  }

  private get hasInvalidOutputLines(): boolean {
    return this.outputLines.some(line => !line.accountId || this.numeric(line.weightKg) <= 0);
  }

  private createEmptyOutputLine(): WashingOutputLineForm {
    return {
      categoryId: null,
      accountId: null,
      weightKg: null
    };
  }

  private getFilteredAccountOptions(accountType: AccountType, categoryId: number | null): SelectOption[] {
    if (!categoryId) {
      return [];
    }

    return [...this.accountsById.values()]
      .filter(account => account.accountType === accountType && account.categoryId === categoryId)
      .map(account => ({ value: account.id, label: account.name }));
  }

  private buildCategoryOptionsForType(accountType: AccountType): SelectOption[] {
    const allowedCategoryIds = new Set(
      [...this.accountsById.values()]
        .filter(account => account.accountType === accountType)
        .map(account => account.categoryId)
    );

    return this.categories
      .filter(category => allowedCategoryIds.has(category.id))
      .map(category => ({ value: category.id, label: category.name }));
  }

  private enforceAllWeightLimits(): void {
    const maxInput = this.getMaxInputWeight();
    if (this.numeric(this.inputWeightKg) > maxInput) {
      this.inputWeightKg = maxInput;
    }

    for (const line of this.outputLines) {
      const maxOutput = this.getMaxOutputWeight(line);
      if (this.numeric(line.weightKg) > maxOutput) {
        line.weightKg = maxOutput;
      }
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
}
