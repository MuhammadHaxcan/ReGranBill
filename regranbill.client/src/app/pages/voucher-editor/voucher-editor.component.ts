import { ChangeDetectorRef, Component, HostListener, OnInit, QueryList, ViewChildren } from '@angular/core';
import { forkJoin } from 'rxjs';
import { SearchableSelectComponent, SelectOption } from '../../components/searchable-select/searchable-select.component';
import { Account } from '../../models/account.model';
import {
  UpdateVoucherEditorRequest,
  VoucherEditorVoucher,
  VoucherType
} from '../../models/voucher-editor.model';
import { AccountService } from '../../services/account.service';
import { VoucherEditorService } from '../../services/voucher-editor.service';

interface EditableLedgerLine {
  id?: number;
  accountId: number | null;
  description: string;
  debit: number;
  credit: number;
  qty: number | null;
  rbp: string | null;
  rate: number | null;
  isEdited: boolean;
}

@Component({
  selector: 'app-voucher-editor',
  templateUrl: './voucher-editor.component.html',
  styleUrl: './voucher-editor.component.css',
  standalone: false
})
export class VoucherEditorComponent implements OnInit {
  @ViewChildren('accountSelect') accountSelects!: QueryList<SearchableSelectComponent>;

  loadingAccounts = true;
  searching = false;
  saving = false;
  errorMessage = '';
  successMessage = '';

  searchVoucherType: VoucherType = 'JournalVoucher';
  searchVoucherNumber = '';

  voucher: VoucherEditorVoucher | null = null;
  voucherDate = new Date();
  description = '';
  vehicleNumber = '';

  accounts: Account[] = [];
  accountOptions: SelectOption[] = [];
  lines: EditableLedgerLine[] = [];

  voucherTypeOptions: SelectOption[] = [
    { value: 'JournalVoucher', label: 'Journal Voucher' },
    { value: 'SaleVoucher', label: 'Sale Voucher' },
    { value: 'CartageVoucher', label: 'Cartage Voucher' },
    { value: 'PurchaseVoucher', label: 'Purchase Voucher' },
    { value: 'ProductionVoucher', label: 'Production Voucher' }
  ];

  rbpOptions: SelectOption[] = [
    { value: 'Yes', label: 'Yes' },
    { value: 'No', label: 'No' }
  ];

  constructor(
    private cdr: ChangeDetectorRef,
    private accountService: AccountService,
    private voucherEditorService: VoucherEditorService
  ) {}

  get voucherDateIso(): string {
    const year = this.voucherDate.getFullYear();
    const month = String(this.voucherDate.getMonth() + 1).padStart(2, '0');
    const day = String(this.voucherDate.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  set voucherDateIso(value: string) {
    if (!value) return;
    this.voucherDate = new Date(value);
  }

  get hasLoadedVoucher(): boolean {
    return this.voucher !== null;
  }

  get isSaleVoucher(): boolean {
    return this.voucher?.voucherType === 'SaleVoucher';
  }

  get totalDebit(): number {
    return this.round2(this.lines.reduce((sum, line) => sum + (line.debit || 0), 0));
  }

  get totalCredit(): number {
    return this.round2(this.lines.reduce((sum, line) => sum + (line.credit || 0), 0));
  }

  get difference(): number {
    return this.round2(this.totalDebit - this.totalCredit);
  }

  get isBalanced(): boolean {
    return this.totalDebit > 0 && this.totalDebit === this.totalCredit;
  }

  get canSave(): boolean {
    return this.hasLoadedVoucher
      && !this.loadingAccounts
      && !this.searching
      && !this.saving
      && this.isBalanced
      && this.hasValidLines();
  }

  ngOnInit(): void {
    this.loadAccounts();
  }

  @HostListener('document:keydown', ['$event'])
  handleKeyboardShortcut(event: KeyboardEvent): void {
    if (!this.hasLoadedVoucher || this.loadingAccounts || this.searching || this.saving) return;

    if (event.altKey && event.key.toLowerCase() === 'n') {
      event.preventDefault();
      this.addLine(true);
    }
  }

  search(): void {
    const voucherNumber = this.searchVoucherNumber.trim();
    if (!voucherNumber) {
      this.errorMessage = 'Enter a voucher number.';
      return;
    }

    this.searching = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.voucherEditorService.search(this.searchVoucherType, voucherNumber).subscribe({
      next: voucher => {
        this.setVoucher(voucher);
        this.searching = false;
        this.cdr.detectChanges();
      },
      error: err => {
        if (err?.status === 404) {
          this.errorMessage = 'Voucher not found for this type and number.';
        } else {
          this.errorMessage = err?.error?.message || 'Unable to load voucher.';
        }
        this.searching = false;
        this.cdr.detectChanges();
      }
    });
  }

  addLine(focusNewLine = false): void {
    this.lines.push(this.newLine());
    if (focusNewLine) {
      this.focusAccountSelector(this.lines.length - 1);
    }
  }

  removeLine(index: number): void {
    if (this.lines.length <= 2) return;
    this.lines.splice(index, 1);
  }

  onAccountChange(line: EditableLedgerLine, accountId: number): void {
    line.accountId = accountId;
  }

  onDebitChange(line: EditableLedgerLine): void {
    line.debit = this.sanitizeAmount(line.debit);
    if (line.debit > 0) {
      line.credit = 0;
    }
  }

  onCreditChange(line: EditableLedgerLine): void {
    line.credit = this.sanitizeAmount(line.credit);
    if (line.credit > 0) {
      line.debit = 0;
    }
  }

  onCreditKeydown(event: KeyboardEvent, lineIndex: number): void {
    if (event.key !== 'Tab' || event.shiftKey) return;

    const nextIndex = lineIndex + 1;
    if (nextIndex >= this.lines.length) {
      return;
    }

    event.preventDefault();
    this.focusAccountSelector(nextIndex);
  }

  onRbpChange(line: EditableLedgerLine, rbpValue: string): void {
    line.rbp = rbpValue;
  }

  save(): void {
    if (!this.voucher || !this.canSave) return;

    this.saving = true;
    this.errorMessage = '';
    this.successMessage = '';

    const request = this.buildUpdateRequest();
    this.voucherEditorService.update(request).subscribe({
      next: voucher => {
        this.setVoucher(voucher);
        this.successMessage = 'Voucher updated successfully.';
        this.saving = false;
        this.cdr.detectChanges();
      },
      error: err => {
        this.errorMessage = err?.error?.message || 'Unable to update voucher.';
        this.saving = false;
        this.cdr.detectChanges();
      }
    });
  }

  private loadAccounts(): void {
    this.loadingAccounts = true;
    this.errorMessage = '';

    forkJoin({
      journal: this.accountService.getJournalAccounts(),
      products: this.accountService.getProducts()
    }).subscribe({
      next: ({ journal, products }) => {
        const merged = [...journal, ...products];
        const uniqueById = new Map<number, Account>();
        for (const account of merged) {
          if (!uniqueById.has(account.id)) {
            uniqueById.set(account.id, account);
          }
        }

        this.accounts = Array.from(uniqueById.values())
          .sort((a, b) => a.name.localeCompare(b.name));

        this.accountOptions = this.accounts.map(account => ({
          value: account.id,
          label: account.name,
          sublabel: this.getAccountSublabel(account)
        }));

        this.loadingAccounts = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.errorMessage = 'Unable to load account lists.';
        this.loadingAccounts = false;
        this.cdr.detectChanges();
      }
    });
  }

  private setVoucher(voucher: VoucherEditorVoucher): void {
    this.voucher = voucher;
    this.searchVoucherType = voucher.voucherType;
    this.searchVoucherNumber = voucher.voucherNumber;
    this.voucherDate = new Date(voucher.date);
    this.description = voucher.description || '';
    this.vehicleNumber = voucher.vehicleNumber || '';
    this.lines = voucher.entries
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map(entry => ({
        id: entry.id,
        accountId: entry.accountId,
        description: entry.description || '',
        debit: entry.debit,
        credit: entry.credit,
        qty: entry.qty ?? null,
        rbp: entry.rbp ?? null,
        rate: entry.rate ?? null,
        isEdited: entry.isEdited
      }));

    while (this.lines.length < 2) {
      this.lines.push(this.newLine());
    }
  }

  private buildUpdateRequest(): UpdateVoucherEditorRequest {
    const voucher = this.voucher!;
    return {
      voucherType: voucher.voucherType,
      voucherNumber: voucher.voucherNumber,
      date: this.voucherDate,
      description: this.description.trim() || null,
      vehicleNumber: this.isSaleVoucher ? (this.vehicleNumber.trim() || null) : null,
      entries: this.lines.map((line, index) => ({
        accountId: line.accountId!,
        description: line.description.trim() || null,
        debit: this.round2(line.debit || 0),
        credit: this.round2(line.credit || 0),
        qty: this.isSaleVoucher ? (line.qty ?? null) : null,
        rbp: this.isSaleVoucher ? (line.rbp ?? null) : null,
        rate: this.isSaleVoucher ? (line.rate ?? null) : null,
        sortOrder: index
      }))
    };
  }

  private hasValidLines(): boolean {
    if (this.lines.length < 2) return false;

    const baseValid = this.lines.every(line => {
      if (line.accountId == null) return false;
      if (line.debit < 0 || line.credit < 0) return false;

      const hasDebit = (line.debit || 0) > 0;
      const hasCredit = (line.credit || 0) > 0;
      return hasDebit !== hasCredit;
    });

    if (!baseValid) return false;
    if (!this.isSaleVoucher) return true;

    const productLines = this.lines.filter(line => this.getAccountType(line.accountId) === 'Product');
    if (productLines.length === 0) return false;

    return productLines.every(line => {
      const validQty = line.qty != null && line.qty > 0;
      const validRbp = line.rbp === 'Yes' || line.rbp === 'No';
      const validRate = line.rate != null && line.rate >= 0;
      return validQty && validRbp && validRate;
    });
  }

  private getAccountType(accountId: number | null): string | null {
    if (accountId == null) return null;
    return this.accounts.find(a => a.id === accountId)?.accountType || null;
  }

  private sanitizeAmount(value: number): number {
    if (!Number.isFinite(value) || value < 0) return 0;
    return this.round2(value);
  }

  private round2(value: number): number {
    return Number((value || 0).toFixed(2));
  }

  private newLine(): EditableLedgerLine {
    return {
      accountId: null,
      description: '',
      debit: 0,
      credit: 0,
      qty: null,
      rbp: null,
      rate: null,
      isEdited: false
    };
  }

  private focusAccountSelector(index: number): void {
    setTimeout(() => {
      const accountSelect = this.accountSelects?.toArray()[index];
      accountSelect?.focusTrigger();
    });
  }

  private getAccountSublabel(account: Account): string {
    if (account.accountType === 'Product') return account.packing || 'Product';
    if (account.accountType === 'Expense') return 'Expense';
    if (account.accountType === 'Account') return account.bankName || 'Cash / Bank';
    if (account.accountType === 'Party') return account.partyRole || 'Party';
    return account.accountType;
  }
}
