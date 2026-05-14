import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { forkJoin } from 'rxjs';
import { AccountService } from '../../services/account.service';
import { FormulationService } from '../../services/formulation.service';
import { ToastService } from '../../services/toast.service';
import { ConfirmModalService } from '../../services/confirm-modal.service';
import { Account, AccountType } from '../../models/account.model';
import {
  CreateFormulationRequest,
  FormulationDto,
  FormulationLineRequest
} from '../../models/formulation.model';
import { ProductionLineKind } from '../../models/production-voucher.model';
import { SelectOption } from '../../components/searchable-select/searchable-select.component';
import { getApiErrorMessage } from '../../utils/api-error';

interface EditableLine {
  lineKind: ProductionLineKind;
  accountId: number | null;
  amountPerBase: number;
  bagsPerBase: number | null;
}

@Component({
  selector: 'app-formulations',
  templateUrl: './formulations.component.html',
  styleUrl: './formulations.component.css',
  standalone: false
})
export class FormulationsComponent implements OnInit {
  formulations: FormulationDto[] = [];
  loading = true;
  saving = false;

  showEditor = false;
  editingId: number | null = null;
  name = '';
  description = '';
  baseInputKg = 40;
  isActive = true;

  inputs: EditableLine[] = [];
  outputs: EditableLine[] = [];
  byproducts: EditableLine[] = [];
  shortage: EditableLine | null = null;

  inputAccountOptions: SelectOption[] = [];
  outputAccountOptions: SelectOption[] = [];
  expenseAccountOptions: SelectOption[] = [];

  constructor(
    private accountService: AccountService,
    private formulationService: FormulationService,
    private toast: ToastService,
    private confirmModal: ConfirmModalService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    this.loading = true;
    forkJoin({
      accounts: this.accountService.getAll(),
      formulations: this.formulationService.getAll()
    }).subscribe({
      next: ({ accounts, formulations }) => {
        const inputAccts = accounts.filter(
          (a: Account) => a.accountType === AccountType.Product || a.accountType === AccountType.RawMaterial
        );
        const outputAccts = accounts.filter((a: Account) => a.accountType === AccountType.Product);
        const expenseAccts = accounts.filter((a: Account) => a.accountType === AccountType.Expense);

        this.inputAccountOptions = inputAccts.map(a => ({ value: a.id, label: a.name, sublabel: a.packing || '' }));
        this.outputAccountOptions = outputAccts.map(a => ({ value: a.id, label: a.name, sublabel: a.packing || '' }));
        this.expenseAccountOptions = expenseAccts.map(a => ({ value: a.id, label: a.name }));

        this.formulations = formulations;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load formulations.');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  // Totals
  get inputTotal(): number {
    return this.round(this.inputs.reduce((s, l) => s + this.numeric(l.amountPerBase), 0));
  }
  get outputTotal(): number {
    const out = this.outputs.reduce((s, l) => s + this.numeric(l.amountPerBase), 0);
    const by = this.byproducts.reduce((s, l) => s + this.numeric(l.amountPerBase), 0);
    const sh = this.shortage ? this.numeric(this.shortage.amountPerBase) : 0;
    return this.round(out + by + sh);
  }
  get base(): number { return this.numeric(this.baseInputKg) || 0; }
  get inputsBalanced(): boolean { return this.base > 0 && Math.abs(this.inputTotal - this.base) <= 0.01; }
  get outputsBalanced(): boolean { return this.base > 0 && Math.abs(this.outputTotal - this.base) <= 0.01; }

  startNew(): void {
    this.editingId = null;
    this.name = '';
    this.description = '';
    this.baseInputKg = 40;
    this.isActive = true;
    this.inputs = [{ lineKind: 'Input', accountId: null, amountPerBase: 40, bagsPerBase: null }];
    this.outputs = [{ lineKind: 'Output', accountId: null, amountPerBase: 34, bagsPerBase: null }];
    this.byproducts = [{ lineKind: 'Byproduct', accountId: null, amountPerBase: 5, bagsPerBase: null }];
    this.shortage = { lineKind: 'Shortage', accountId: null, amountPerBase: 1, bagsPerBase: null };
    this.showEditor = true;
  }

  edit(formulation: FormulationDto): void {
    this.editingId = formulation.id;
    this.name = formulation.name;
    this.description = formulation.description || '';
    this.baseInputKg = formulation.baseInputKg;
    this.isActive = formulation.isActive;

    const toLine = (l: any): EditableLine => ({
      lineKind: l.lineKind,
      accountId: l.accountId,
      amountPerBase: l.amountPerBase,
      bagsPerBase: l.bagsPerBase ?? null
    });

    this.inputs = formulation.lines.filter(l => l.lineKind === 'Input').map(toLine);
    this.outputs = formulation.lines.filter(l => l.lineKind === 'Output').map(toLine);
    this.byproducts = formulation.lines.filter(l => l.lineKind === 'Byproduct').map(toLine);
    const shortageLine = formulation.lines.find(l => l.lineKind === 'Shortage');
    this.shortage = shortageLine ? toLine(shortageLine) : null;

    if (this.inputs.length === 0) this.inputs = [{ lineKind: 'Input', accountId: null, amountPerBase: 0, bagsPerBase: null }];
    if (this.outputs.length === 0) this.outputs = [{ lineKind: 'Output', accountId: null, amountPerBase: 0, bagsPerBase: null }];

    this.showEditor = true;
  }

  cancelEdit(): void {
    this.showEditor = false;
    this.editingId = null;
  }

  addInput(): void { this.inputs.push({ lineKind: 'Input', accountId: null, amountPerBase: 0, bagsPerBase: null }); }
  addOutput(): void { this.outputs.push({ lineKind: 'Output', accountId: null, amountPerBase: 0, bagsPerBase: null }); }
  addByproduct(): void { this.byproducts.push({ lineKind: 'Byproduct', accountId: null, amountPerBase: 0, bagsPerBase: null }); }
  enableShortage(): void {
    if (!this.shortage) {
      this.shortage = { lineKind: 'Shortage', accountId: null, amountPerBase: 0, bagsPerBase: null };
    }
  }
  removeShortage(): void { this.shortage = null; }
  removeLine(list: EditableLine[], i: number): void { list.splice(i, 1); }

  get canSave(): boolean {
    if (this.saving) return false;
    if (!this.name.trim()) return false;
    if (this.base <= 0) return false;
    if (!this.inputsBalanced) return false;
    if (!this.outputsBalanced) return false;
    return true;
  }

  save(): void {
    if (!this.canSave) {
      if (!this.name.trim()) this.toast.error('Formulation name is required.');
      else if (this.base <= 0) this.toast.error('Base input kg must be greater than zero.');
      else if (!this.inputsBalanced) this.toast.error(`Input amounts must total ${this.base}. Current: ${this.inputTotal}.`);
      else if (!this.outputsBalanced) this.toast.error(`Output + byproduct + shortage amounts must total ${this.base}. Current: ${this.outputTotal}.`);
      return;
    }

    const lines: FormulationLineRequest[] = [];
    let sortOrder = 0;
    const push = (list: EditableLine[]) => {
      for (const l of list) {
        if (!l.accountId) continue;
        lines.push({
          lineKind: l.lineKind,
          accountId: l.accountId,
          amountPerBase: this.numeric(l.amountPerBase),
          bagsPerBase: l.bagsPerBase !== null && l.bagsPerBase !== undefined && this.numeric(l.bagsPerBase) > 0
            ? this.numeric(l.bagsPerBase)
            : null,
          sortOrder: ++sortOrder
        });
      }
    };
    push(this.inputs);
    push(this.outputs);
    push(this.byproducts);
    if (this.shortage && this.shortage.accountId) {
      lines.push({
        lineKind: 'Shortage',
        accountId: this.shortage.accountId,
        amountPerBase: this.numeric(this.shortage.amountPerBase),
        bagsPerBase: null,
        sortOrder: ++sortOrder
      });
    }

    const req: CreateFormulationRequest = {
      name: this.name.trim(),
      description: this.description || null,
      baseInputKg: this.base,
      isActive: this.isActive,
      lines
    };

    this.saving = true;
    const request$ = this.editingId
      ? this.formulationService.update(this.editingId, req)
      : this.formulationService.create(req);

    request$.subscribe({
      next: () => {
        this.saving = false;
        this.toast.success(this.editingId ? 'Formulation updated.' : 'Formulation created.');
        this.showEditor = false;
        this.editingId = null;
        this.loadAll();
      },
      error: err => {
        this.saving = false;
        this.toast.error(getApiErrorMessage(err, 'Unable to save formulation.'));
      }
    });
  }

  async deleteFormulation(formulation: FormulationDto): Promise<void> {
    const confirmed = await this.confirmModal.confirm({
      title: 'Delete Formulation',
      message: `Delete formulation "${formulation.name}"?`,
      confirmText: 'Delete',
      cancelText: 'Cancel'
    });
    if (!confirmed) return;

    this.formulationService.delete(formulation.id).subscribe({
      next: () => {
        this.toast.success('Formulation deleted.');
        this.loadAll();
      },
      error: err => this.toast.error(getApiErrorMessage(err, 'Unable to delete formulation.'))
    });
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
