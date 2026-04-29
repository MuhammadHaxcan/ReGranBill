import { ChangeDetectorRef, Component, HostListener, OnInit, QueryList, ViewChildren } from '@angular/core';
import { forkJoin, Observable } from 'rxjs';
import { SearchableSelectComponent, SelectOption } from '../../components/searchable-select/searchable-select.component';
import { Account, AccountType, PartyRole } from '../../models/account.model';
import { Category } from '../../models/category.model';
import {
  UpdateVoucherEditorRequest,
  VoucherEditorVoucher,
  VoucherType
} from '../../models/voucher-editor.model';
import { AccountService } from '../../services/account.service';
import { CategoryService } from '../../services/category.service';
import { VoucherEditorService } from '../../services/voucher-editor.service';
import { ToastService } from '../../services/toast.service';
import {
  getDeliveryLineAmount,
  getPurchaseLineAmount,
  getPurchaseLineAverageWeight as calculatePurchaseLineAverageWeight,
  round2
} from '../../utils/delivery-calculations';
import { parseLocalDate, toDateInputValue } from '../../utils/date-utils';

interface EditableLedgerLine {
  id?: number;
  categoryId: number | null;
  accountId: number | null;
  description: string;
  debit: number;
  credit: number;
  qty: number | null;
  totalWeightKg: number | null;
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

  searchVoucherType: VoucherType = 'JournalVoucher';
  searchVoucherNumber = '';

  voucher: VoucherEditorVoucher | null = null;
  voucherDate = new Date();
  description = '';
  vehicleNumber = '';

  accounts: Account[] = [];
  categoryOptions: SelectOption[] = [];
  accountOptions: SelectOption[] = [];
  lines: EditableLedgerLine[] = [];

  private categories: Category[] = [];
  // per-category account cache, keyed by categoryId
  accountsByCategory = new Map<number, Account[]>();
  // all accounts for categoryId resolution
  allAccounts: Account[] = [];
  // track in-flight API calls per categoryId
  private categoryAccountRequests = new Map<number, Observable<Account[]>>();

  voucherTypeOptions: SelectOption[] = [
    { value: 'JournalVoucher', label: 'Journal Voucher' },
    { value: 'ReceiptVoucher', label: 'Receipt Voucher' },
    { value: 'PaymentVoucher', label: 'Payment Voucher' },
    { value: 'SaleVoucher', label: 'Sale Voucher' },
    { value: 'SaleReturnVoucher', label: 'Sale Return Voucher' },
    { value: 'CartageVoucher', label: 'Cartage Voucher' },
    { value: 'PurchaseVoucher', label: 'Purchase Voucher' }
  ];

  rbpOptions: SelectOption[] = [
    { value: 'Yes', label: 'Yes' },
    { value: 'No', label: 'No' }
  ];

  constructor(
    private cdr: ChangeDetectorRef,
    private accountService: AccountService,
    private categoryService: CategoryService,
    private voucherEditorService: VoucherEditorService,
    private toast: ToastService
  ) {}

  get voucherDateIso(): string {
    return toDateInputValue(this.voucherDate);
  }

  set voucherDateIso(value: string) {
    if (!value) return;
    this.voucherDate = parseLocalDate(value);
  }

  get hasLoadedVoucher(): boolean {
    return this.voucher !== null;
  }

  get isSaleVoucher(): boolean {
    return this.voucher?.voucherType === 'SaleVoucher';
  }

  get isPurchaseVoucher(): boolean {
    return this.voucher?.voucherType === 'PurchaseVoucher';
  }

  get supportsVehicleNumber(): boolean {
    return this.isSaleVoucher || this.isPurchaseVoucher;
  }

  get totalDebit(): number {
    return round2(this.lines.reduce((sum, line) => sum + (line.debit || 0), 0));
  }

  get totalCredit(): number {
    return round2(this.lines.reduce((sum, line) => sum + (line.credit || 0), 0));
  }

  get difference(): number {
    return round2(this.totalDebit - this.totalCredit);
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
      && this.hasValidLines()
      && !this.validationMessage;
  }

  get validationMessage(): string | null {
    if (!this.hasValidLines()) {
      return 'Each line must have one valid debit or credit amount with required inventory fields.';
    }

    if (this.isSaleVoucher) {
      return this.getSaleVoucherValidationError();
    }

    if (this.isPurchaseVoucher) {
      return this.getPurchaseVoucherValidationError();
    }

    return null;
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
      this.toast.error('Enter a voucher number.');
      return;
    }

    this.searching = true;

    this.voucherEditorService.search(this.searchVoucherType, voucherNumber).subscribe({
      next: voucher => {
        this.setVoucher(voucher);
        this.searching = false;
        this.cdr.detectChanges();
      },
      error: err => {
        if (err?.status === 404) {
          this.toast.error('Voucher not found for this type and number.');
        } else {
          this.toast.error(err?.error?.message || 'Unable to load voucher.');
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
    if (!this.voucher) return;

    if (this.validationMessage) {
      this.toast.error(this.validationMessage);
      return;
    }

    if (!this.canSave) return;

    this.saving = true;

    const request = this.buildUpdateRequest();
    this.voucherEditorService.update(request).subscribe({
      next: voucher => {
        this.setVoucher(voucher);
        this.toast.success(`${voucher.voucherNumber} updated successfully.`);
        this.saving = false;
        this.cdr.detectChanges();
      },
      error: err => {
        this.toast.error(err?.error?.message || 'Unable to update voucher.');
        this.saving = false;
        this.cdr.detectChanges();
      }
    });
  }

  private loadAccounts(): void {
    this.loadingAccounts = true;

    forkJoin({
      allAccounts: this.accountService.getAll(),
      categories: this.categoryService.getAll()
    }).subscribe({
      next: ({ allAccounts, categories }) => {
        this.categories = categories;
        this.categoryOptions = categories.map(c => ({ value: c.id, label: c.name }));

        const uniqueById = new Map<number, Account>();
        for (const account of allAccounts) {
          if (!uniqueById.has(account.id)) {
            uniqueById.set(account.id, account);
          }
        }

        this.allAccounts = Array.from(uniqueById.values())
          .sort((a, b) => a.name.localeCompare(b.name));

        this.accountOptions = this.allAccounts.map(account => ({
          value: account.id,
          label: account.name,
          sublabel: this.getAccountSublabel(account)
        }));

        this.loadingAccounts = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load account lists.');
        this.loadingAccounts = false;
        this.cdr.detectChanges();
      }
    });
  }

  private loadAccountsForCategory(categoryId: number): void {
    if (this.accountsByCategory.has(categoryId)) return;
    if (this.categoryAccountRequests.has(categoryId)) return;

    const request = this.accountService.getByCategory(categoryId);
    this.categoryAccountRequests.set(categoryId, request);

    request.subscribe({
      next: accounts => {
        this.accountsByCategory.set(categoryId, accounts);
        this.categoryAccountRequests.delete(categoryId);
        this.cdr.detectChanges();
      },
      error: () => {
        this.categoryAccountRequests.delete(categoryId);
        this.toast.error('Unable to load accounts for selected category.');
      }
    });
  }

  private setVoucher(voucher: VoucherEditorVoucher): void {
    this.voucher = voucher;
    this.searchVoucherType = voucher.voucherType;
    this.searchVoucherNumber = voucher.voucherNumber;
    this.voucherDate = parseLocalDate(voucher.date);
    this.description = voucher.description || '';
    this.vehicleNumber = voucher.vehicleNumber || '';
    this.lines = voucher.entries
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map(entry => {
        const account = this.allAccounts.find(a => a.id === entry.accountId);
        return {
          id: entry.id,
          categoryId: account?.categoryId ?? null,
          accountId: entry.accountId,
          description: entry.description || '',
          debit: entry.debit,
          credit: entry.credit,
          qty: entry.qty ?? null,
          totalWeightKg: entry.totalWeightKg ?? null,
          rbp: entry.rbp ?? null,
          rate: entry.rate ?? null,
          isEdited: entry.isEdited
        };
      });

    while (this.lines.length < 2) {
      this.lines.push(this.newLine());
    }

    // Prime account cache for each category present in the voucher
    const uniqueCategoryIds = [...new Set(this.lines.map(l => l.categoryId).filter(id => id != null) as number[])];
    for (const catId of uniqueCategoryIds) {
      this.loadAccountsForCategory(catId);
    }
  }

  private buildUpdateRequest(): UpdateVoucherEditorRequest {
    const voucher = this.voucher!;
    return {
      voucherType: voucher.voucherType,
      voucherNumber: voucher.voucherNumber,
      date: this.voucherDate,
      description: this.description.trim() || null,
      vehicleNumber: this.supportsVehicleNumber ? (this.vehicleNumber.trim() || null) : null,
      entries: this.lines.map((line, index) => ({
        accountId: line.accountId!,
        description: line.description.trim() || null,
        debit: round2(line.debit || 0),
        credit: round2(line.credit || 0),
        qty: this.supportsInventoryMeta && this.isInventoryAccountId(line.accountId) ? (line.qty ?? null) : null,
        totalWeightKg: this.isPurchaseVoucher && this.isInventoryAccountId(line.accountId) ? (line.totalWeightKg ?? null) : null,
        rbp: this.isSaleVoucher && this.isInventoryAccountId(line.accountId) ? (line.rbp ?? null) : null,
        rate: this.supportsInventoryMeta && this.isInventoryAccountId(line.accountId) ? (line.rate ?? null) : null,
        sortOrder: index
      }))
    };
  }

  private hasValidLines(): boolean {
    if (this.lines.length < 2) return false;

    const baseValid = this.lines.every(line => {
      if (line.categoryId == null) return false;
      if (line.accountId == null) return false;
      if (line.debit < 0 || line.credit < 0) return false;

      const hasDebit = (line.debit || 0) > 0;
      const hasCredit = (line.credit || 0) > 0;
      return hasDebit !== hasCredit;
    });

    if (!baseValid) return false;

    if (this.isSaleVoucher) {
      const productLines = this.lines.filter(line => this.isInventoryLine(line));
      if (productLines.length === 0) return false;

      return productLines.every(line => {
        const validQty = line.qty != null && line.qty > 0;
        const validRbp = line.rbp === 'Yes' || line.rbp === 'No';
        const validRate = line.rate != null && line.rate >= 0;
        return validQty && validRbp && validRate;
      });
    }

    if (this.isPurchaseVoucher) {
      const productLines = this.lines.filter(line => this.isInventoryLine(line));
      if (productLines.length === 0) return false;

      return productLines.every(line => {
        const validQty = line.qty != null && line.qty > 0;
        const validTotalWeight = line.totalWeightKg != null && line.totalWeightKg > 0;
        const validRate = line.rate != null && line.rate >= 0;
        return validQty && validTotalWeight && validRate;
      });
    }

    return true;
  }

  isSaleProductLine(line: EditableLedgerLine): boolean {
    return this.isInventoryLine(line);
  }

  isPurchaseProductLine(line: EditableLedgerLine): boolean {
    return this.isInventoryLine(line);
  }

  getExpectedSaleLineAmount(line: EditableLedgerLine): number | null {
    if (!this.isSaleVoucher || !this.isSaleProductLine(line)) return null;
    if (line.qty == null || line.qty <= 0 || (line.rbp !== 'Yes' && line.rbp !== 'No') || line.rate == null || line.rate < 0) {
      return null;
    }

    const account = this.getAccount(line.accountId);
    return round2(getDeliveryLineAmount({
      qty: line.qty,
      rate: line.rate,
      rbp: line.rbp,
      packingWeightKg: account?.packingWeightKg ?? 0
    }));
  }

  getExpectedPurchaseLineAmount(line: EditableLedgerLine): number | null {
    if (!this.isPurchaseVoucher || !this.isPurchaseProductLine(line)) return null;
    if (line.qty == null || line.qty <= 0 || line.totalWeightKg == null || line.totalWeightKg <= 0 || line.rate == null || line.rate < 0) {
      return null;
    }

    return round2(getPurchaseLineAmount({
      totalWeightKg: line.totalWeightKg,
      rate: line.rate
    }));
  }

  getPurchaseLineAverageWeight(line: EditableLedgerLine): number | null {
    if (!this.isPurchaseVoucher || !this.isPurchaseProductLine(line)) return null;
    if (line.qty == null || line.qty <= 0 || line.totalWeightKg == null || line.totalWeightKg <= 0) {
      return null;
    }

    return round2(calculatePurchaseLineAverageWeight({
      qty: line.qty,
      totalWeightKg: line.totalWeightKg
    }));
  }

  isSaleLineAmountMismatch(line: EditableLedgerLine): boolean {
    const expected = this.getExpectedSaleLineAmount(line);
    if (expected == null) return false;
    return round2(line.credit || 0) !== expected;
  }

  isPurchaseLineAmountMismatch(line: EditableLedgerLine): boolean {
    const expected = this.getExpectedPurchaseLineAmount(line);
    if (expected == null) return false;
    return round2(line.debit || 0) !== expected;
  }

  getSaleCustomerExpectedDebit(): number {
    return round2(this.lines
      .filter(line => this.isSaleProductLine(line))
      .reduce((sum, line) => sum + (this.getExpectedSaleLineAmount(line) ?? 0), 0));
  }

  getPurchaseVendorExpectedCredit(): number {
    return round2(this.lines
      .filter(line => this.isPurchaseProductLine(line))
      .reduce((sum, line) => sum + (this.getExpectedPurchaseLineAmount(line) ?? 0), 0));
  }

  isSaleCustomerLine(line: EditableLedgerLine): boolean {
    const account = this.getAccount(line.accountId);
    return account?.accountType === AccountType.Party
      && (account.partyRole === PartyRole.Customer || account.partyRole === PartyRole.Both);
  }

  isPurchaseVendorLine(line: EditableLedgerLine): boolean {
    const account = this.getAccount(line.accountId);
    return account?.accountType === AccountType.Party
      && (account.partyRole === PartyRole.Vendor || account.partyRole === PartyRole.Both);
  }

  private getAccountType(accountId: number | null): AccountType | null {
    if (accountId == null) return null;
    return this.allAccounts.find(a => a.id === accountId)?.accountType || null;
  }

  private getAccount(accountId: number | null): Account | undefined {
    if (accountId == null) return undefined;
    return this.allAccounts.find(account => account.id === accountId);
  }

  private isInventoryLine(line: EditableLedgerLine): boolean {
    const accountType = this.getAccountType(line.accountId);
    return accountType === AccountType.Product || accountType === AccountType.RawMaterial;
  }

  private isInventoryAccountId(accountId: number | null): boolean {
    const accountType = this.getAccountType(accountId);
    return accountType === AccountType.Product || accountType === AccountType.RawMaterial;
  }

  private sanitizeAmount(value: number): number {
    if (!Number.isFinite(value) || value < 0) return 0;
    return round2(value);
  }

  private get supportsInventoryMeta(): boolean {
    return this.isSaleVoucher || this.isPurchaseVoucher;
  }



  private newLine(): EditableLedgerLine {
    return {
      categoryId: null,
      accountId: null,
      description: '',
      debit: 0,
      credit: 0,
      qty: null,
      totalWeightKg: null,
      rbp: null,
      rate: null,
      isEdited: false
    };
  }

  getAccountOptionsForLine(line: EditableLedgerLine): SelectOption[] {
    if (line.categoryId == null) return [];
    const accounts = this.accountsByCategory.get(line.categoryId);
    if (!accounts) return [];
    return accounts.map(account => ({
      value: account.id,
      label: account.name,
      sublabel: this.getAccountSublabel(account)
    }));
  }

  onCategoryChange(line: EditableLedgerLine, categoryId: number): void {
    line.categoryId = categoryId;
    line.accountId = null;
    this.loadAccountsForCategory(categoryId);
  }

  private focusAccountSelector(index: number): void {
    setTimeout(() => {
      const accountSelect = this.accountSelects?.toArray()[index];
      accountSelect?.focusTrigger();
    });
  }

  private getAccountSublabel(account: Account): string {
    if (account.accountType === 'Product') return account.packing || 'Product';
    if (account.accountType === 'RawMaterial') return account.packing || 'Raw material';
    if (account.accountType === 'Expense') return 'Expense';
    if (account.accountType === 'Account') return account.bankName || 'Cash / Bank';
    if (account.accountType === 'Party') return account.partyRole || 'Party';
    return account.accountType;
  }

  private getSaleVoucherValidationError(): string | null {
    const productLines = this.lines.filter(line => this.isSaleProductLine(line));
    if (productLines.length === 0) {
      return 'Sale voucher must contain at least one product line.';
    }

    for (const line of productLines) {
      const expectedAmount = this.getExpectedSaleLineAmount(line);
      if (expectedAmount == null) {
        return 'Every sale product line must have qty, RBP, and rate before saving.';
      }

      if ((line.debit || 0) > 0) {
        return `${this.getAccount(line.accountId)?.name || 'Inventory line'} must remain a credit line.`;
      }

      if (round2(line.credit || 0) !== expectedAmount) {
        return `${this.getAccount(line.accountId)?.name || 'Inventory line'} credit must be ${expectedAmount.toFixed(2)} based on qty, RBP, packing weight, and rate.`;
      }
    }

    const customerLines = this.lines.filter(line => this.isSaleCustomerLine(line) && (line.debit || 0) > 0);
    if (customerLines.length !== 1) {
      return 'Sale voucher must have exactly one customer debit line.';
    }

    if (this.lines[0] !== customerLines[0]) {
      return 'Customer debit line must remain the first line in the sale voucher.';
    }

    const expectedDebit = this.getSaleCustomerExpectedDebit();
    const actualDebit = round2(customerLines[0].debit || 0);
    if (actualDebit !== expectedDebit) {
      return `Customer debit must equal total product amount of ${expectedDebit.toFixed(2)}.`;
    }

    const nonProductCredits = this.lines.filter(line => (line.credit || 0) > 0 && !this.isSaleProductLine(line));
    if (nonProductCredits.length > 0) {
      return 'Sale voucher credit lines must be inventory accounts only.';
    }

    return null;
  }

  private getPurchaseVoucherValidationError(): string | null {
    const productLines = this.lines.filter(line => this.isPurchaseProductLine(line));
    if (productLines.length === 0) {
      return 'Purchase voucher must contain at least one product line.';
    }

    for (const line of productLines) {
      const expectedAmount = this.getExpectedPurchaseLineAmount(line);
      if (expectedAmount == null) {
        return 'Every purchase product line must have bags, total kg, and rate before saving.';
      }

      if ((line.credit || 0) > 0) {
        return `${this.getAccount(line.accountId)?.name || 'Inventory line'} must remain a debit line.`;
      }

      if (round2(line.debit || 0) !== expectedAmount) {
        return `${this.getAccount(line.accountId)?.name || 'Inventory line'} debit must be ${expectedAmount.toFixed(2)} based on total kg and rate.`;
      }
    }

    const vendorLines = this.lines.filter(line => this.isPurchaseVendorLine(line) && (line.credit || 0) > 0);
    if (vendorLines.length !== 1) {
      return 'Purchase voucher must have exactly one vendor credit line.';
    }

    if (this.lines[0] !== vendorLines[0]) {
      return 'Vendor credit line must remain the first line in the purchase voucher.';
    }

    const expectedCredit = this.getPurchaseVendorExpectedCredit();
    const actualCredit = round2(vendorLines[0].credit || 0);
    if (actualCredit !== expectedCredit) {
      return `Vendor credit must equal total product amount of ${expectedCredit.toFixed(2)}.`;
    }

    const nonProductDebits = this.lines.filter(line => (line.debit || 0) > 0 && !this.isPurchaseProductLine(line));
    if (nonProductDebits.length > 0) {
      return 'Purchase voucher debit lines must be inventory accounts only.';
    }

    return null;
  }
}
