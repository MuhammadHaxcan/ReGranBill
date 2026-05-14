import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { AccountService } from '../../services/account.service';
import { WashingVoucherService } from '../../services/washing-voucher.service';
import { ToastService } from '../../services/toast.service';
import { Account, AccountType } from '../../models/account.model';
import {
  CreateWashingVoucherOutputLineRequest,
  CreateWashingVoucherRequest
} from '../../models/washing-voucher.model';
import { SelectOption } from '../../components/searchable-select/searchable-select.component';
import { toDateInputValue } from '../../utils/date-utils';
import { getApiErrorMessage } from '../../utils/api-error';

const DEFAULT_WASTAGE_THRESHOLD_PCT = 10;

interface WashingOutputLineForm {
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
  voucherNumber = '';
  voucherDate = new Date();
  description = '';

  sourceVendorId: number | null = null;
  unwashedAccountId: number | null = null;
  inputWeightKg = 0;
  thresholdPct = DEFAULT_WASTAGE_THRESHOLD_PCT;
  outputLines: WashingOutputLineForm[] = [this.createEmptyOutputLine()];

  vendorOptions: SelectOption[] = [];
  unwashedAccountOptions: SelectOption[] = [];
  outputAccountOptions: SelectOption[] = [];

  accountsById = new Map<number, Account>();
  rate = 0;
  rateSource: string | null = null;

  loading = true;
  saving = false;

  constructor(
    private accountService: AccountService,
    private washingService: WashingVoucherService,
    private toast: ToastService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    forkJoin({
      accounts: this.accountService.getAll(),
      next: this.washingService.getNextNumber()
    }).subscribe({
      next: ({ accounts, next }) => {
        this.voucherNumber = next;
        this.accountsById = new Map(accounts.map(a => [a.id, a]));
        this.vendorOptions = accounts
          .filter(a => a.accountType === AccountType.Party)
          .map(a => ({ value: a.id, label: a.name }));
        this.unwashedAccountOptions = accounts
          .filter(a => a.accountType === AccountType.UnwashedMaterial)
          .map(a => ({ value: a.id, label: a.name }));
        this.outputAccountOptions = accounts
          .filter(a => a.accountType === AccountType.RawMaterial)
          .map(a => ({ value: a.id, label: a.name }));
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

  onVendorOrUnwashedChanged(): void {
    if (!this.sourceVendorId || !this.unwashedAccountId) {
      this.rate = 0;
      this.rateSource = null;
      return;
    }
    this.washingService.getLatestUnwashedRate(this.sourceVendorId, this.unwashedAccountId).subscribe({
      next: res => {
        if (res) {
          this.rate = res.rate;
          this.rateSource = `${res.sourceVoucherNumber} · ${res.sourceDate}`;
        } else {
          this.rate = 0;
          this.rateSource = 'No prior purchase from this vendor';
        }
        this.cdr.detectChanges();
      },
      error: () => {
        this.rate = 0;
        this.rateSource = null;
      }
    });
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
    if (this.numeric(this.inputWeightKg) <= 0) return false;
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

  save(): void {
    if (!this.canSave) {
      if (!this.sourceVendorId) this.toast.error('Pick a source vendor.');
      else if (!this.unwashedAccountId) this.toast.error('Pick an unwashed material account.');
      else if (this.rate <= 0) this.toast.error('No purchase rate found for vendor + unwashed material.');
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
      inputWeightKg: this.numeric(this.inputWeightKg),
      outputWeightKg: this.totalOutputWeightKg,
      outputLines,
      thresholdPct: this.effectiveThresholdPct
    };

    this.saving = true;
    this.washingService.create(req).subscribe({
      next: v => {
        this.saving = false;
        this.toast.success(`${v.voucherNumber} saved · washed rate ${v.washedRate.toFixed(2)}/kg`
          + (v.excessWastageKg > 0 ? `, ${v.excessWastageKg.toFixed(2)} kg excess charged to vendor` : ''));
        this.router.navigate(['/washing-voucher']);
        this.resetForm();
      },
      error: err => {
        this.saving = false;
        this.toast.error(getApiErrorMessage(err, 'Unable to save washing voucher.'));
      }
    });
  }

  private resetForm(): void {
    this.voucherDate = new Date();
    this.description = '';
    this.sourceVendorId = null;
    this.unwashedAccountId = null;
    this.inputWeightKg = 0;
    this.thresholdPct = DEFAULT_WASTAGE_THRESHOLD_PCT;
    this.outputLines = [this.createEmptyOutputLine()];
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
    return this.outputLines.some(line =>
      !line.accountId || this.numeric(line.weightKg) <= 0);
  }

  private createEmptyOutputLine(): WashingOutputLineForm {
    return {
      accountId: null,
      weightKg: null
    };
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
