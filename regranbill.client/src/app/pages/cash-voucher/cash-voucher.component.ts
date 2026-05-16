import { ChangeDetectorRef, Component, HostListener, OnInit, QueryList, ViewChildren } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { forkJoin } from 'rxjs';
import { map } from 'rxjs/operators';
import { SearchableSelectComponent, SelectOption } from '../../components/searchable-select/searchable-select.component';
import { Account, AccountType, PartyRole } from '../../models/account.model';
import { CashVoucher, CashVoucherMode, CreateCashVoucherRequest } from '../../models/cash-voucher.model';
import { AccountService } from '../../services/account.service';
import { CashVoucherService } from '../../services/cash-voucher.service';
import { CategoryService } from '../../services/category.service';
import { Category } from '../../models/category.model';
import { ToastService } from '../../services/toast.service';
import { round2 } from '../../utils/delivery-calculations';
import { getApiErrorMessage } from '../../utils/api-error';
import { toDateInputValue } from '../../utils/date-utils';

interface EditableCashVoucherLine {
  id?: number;
  categoryId: number | null;
  accountId: number | null;
  description: string;
  amount: number;
  isEdited: boolean;
}

@Component({
  selector: 'app-cash-voucher',
  templateUrl: './cash-voucher.component.html',
  styleUrl: './cash-voucher.component.css',
  standalone: false
})
export class CashVoucherComponent implements OnInit {
  @ViewChildren('accountSelect') accountSelects!: QueryList<SearchableSelectComponent>;

  mode: CashVoucherMode = 'receipt';
  voucherId: number | null = null;
  isEditMode = false;
  loading = true;
  saving = false;

  voucherNumber = '';
  voucherDate = new Date();
  description = '';
  partyAccountId: number | null = null;

  partyAccounts: Account[] = [];
  cashAccounts: Account[] = [];
  partyOptions: SelectOption[] = [];
  cashAccountOptions: SelectOption[] = [];
  cashCategoryOptions: SelectOption[] = [];
  lines: EditableCashVoucherLine[] = [];

  private accountsById = new Map<number, Account>();

  // Add account modal
  showAddAccountModal = false;
  addAccountPrefillName = '';
  addAccountDefaultType: AccountType | undefined;
  addAccountDefaultRole: PartyRole | undefined;
  addAccountDefaultCategoryId: number | undefined;
  private addAccountContext: 'party' | 'cash' = 'party';
  private addAccountTargetLine: EditableCashVoucherLine | null = null;

  constructor(
    private route: ActivatedRoute,
    private cdr: ChangeDetectorRef,
    private accountService: AccountService,
    private cashVoucherService: CashVoucherService,
    private categoryService: CategoryService,
    private toast: ToastService
  ) {}

  get pageTitle(): string {
    return this.mode === 'receipt' ? 'Receipt Voucher' : 'Payment Voucher';
  }

  get pageSubtitle(): string {
    return this.mode === 'receipt'
      ? 'Select the customer first, then enter the cash or bank accounts where money was received.'
      : 'Select the vendor first, then enter the cash or bank accounts from which money was paid.';
  }

  get partyLabel(): string {
    return this.mode === 'receipt' ? 'Customer' : 'Vendor';
  }

  get lineTitle(): string {
    return this.mode === 'receipt' ? 'Receipt Lines' : 'Payment Lines';
  }

  get lineAccountLabel(): string {
    return this.mode === 'receipt' ? 'Received In' : 'Paid From';
  }

  get accountingHint(): string {
    return this.mode === 'receipt'
      ? 'Customer will be credited automatically. Each cash or bank line will be debited.'
      : 'Vendor will be debited automatically. Each cash or bank line will be credited.';
  }

  get totalAmount(): number {
    return round2(this.lines.reduce((sum, line) => sum + (line.amount || 0), 0));
  }

  get canSave(): boolean {
    return !this.loading
      && !this.saving
      && this.partyAccountId != null
      && this.totalAmount > 0
      && this.hasValidLines();
  }

  ngOnInit(): void {
    this.mode = (this.route.snapshot.data['mode'] as CashVoucherMode) || 'receipt';
    this.loadData();
  }

  onAddPartyClicked(prefillName: string): void {
    this.addAccountPrefillName = prefillName;
    this.addAccountDefaultType = AccountType.Party;
    this.addAccountDefaultRole = this.mode === 'receipt' ? PartyRole.Customer : PartyRole.Vendor;
    this.addAccountContext = 'party';
    this.showAddAccountModal = true;
  }

  onAddCashAccountClicked(prefillName: string, line: EditableCashVoucherLine): void {
    this.addAccountPrefillName = prefillName;
    this.addAccountDefaultType = AccountType.Account;
    this.addAccountDefaultRole = undefined;
    this.addAccountDefaultCategoryId = line.categoryId ?? undefined;
    this.addAccountContext = 'cash';
    this.addAccountTargetLine = line;
    this.showAddAccountModal = true;
  }

  onAddAccountModalClosed(): void {
    this.showAddAccountModal = false;
  }

  onNewAccountCreated(account: Account): void {
    this.showAddAccountModal = false;

    if (this.addAccountContext === 'party') {
      this.partyAccounts = [...this.partyAccounts, account];
      this.partyOptions = this.partyAccounts.map(a => ({
        value: a.id,
        label: a.name,
        sublabel: a.city || a.partyRole || 'Party'
      }));
      this.partyAccountId = account.id;
    } else {
      this.cashAccounts = [...this.cashAccounts, account];
      this.accountsById.set(account.id, account);
      this.cashAccountOptions = this.cashAccounts.map(a => ({
        value: a.id,
        label: a.name,
        sublabel: a.bankName || 'Cash / Bank'
      }));
      if (account.categoryId && !this.cashCategoryOptions.some(c => c.value === account.categoryId)) {
        // The new account's category may not yet be in the filtered list; refresh from server.
        this.categoryService.getFiltered([AccountType.Account]).subscribe(cats => {
          this.cashCategoryOptions = cats.map(c => ({ value: c.id, label: c.name }));
          this.cdr.detectChanges();
        });
      }
      const target = this.addAccountTargetLine ?? this.lines.find(l => l.accountId == null) ?? null;
      if (target) {
        target.categoryId = account.categoryId ?? null;
        target.accountId = account.id;
      }
      this.addAccountTargetLine = null;
    }

    this.cdr.detectChanges();
  }

  @HostListener('document:keydown', ['$event'])
  handleKeyboardShortcut(event: KeyboardEvent): void {
    if (this.loading || this.saving) return;
    if (event.altKey && event.key.toLowerCase() === 'n') {
      event.preventDefault();
      this.addLine(true);
    }
  }

  addLine(focusNewLine = false): void {
    this.lines.push(this.newLine());
    if (focusNewLine) {
      this.focusAccountSelector(this.lines.length - 1);
    }
  }

  removeLine(index: number): void {
    if (this.lines.length <= 1) return;
    this.lines.splice(index, 1);
  }

  onPartyChange(accountId: number): void {
    this.partyAccountId = accountId;
  }

  onAccountChange(line: EditableCashVoucherLine, accountId: number): void {
    line.accountId = accountId;
  }

  onAmountChange(line: EditableCashVoucherLine): void {
    line.amount = this.sanitizeAmount(line.amount);
  }

  onAmountKeydown(event: KeyboardEvent, lineIndex: number): void {
    if (event.key !== 'Tab' || event.shiftKey) return;

    const nextIndex = lineIndex + 1;
    if (nextIndex >= this.lines.length) {
      return;
    }

    event.preventDefault();
    this.focusAccountSelector(nextIndex);
  }

  save(): void {
    if (!this.canSave) return;

    const request = this.buildRequest();
    this.saving = true;

    const isEdit = this.isEditMode && this.voucherId !== null;
    const request$ = isEdit
      ? this.cashVoucherService.update(this.mode, this.voucherId!, request)
      : this.cashVoucherService.create(this.mode, request);
    const fallbackMessage = isEdit
      ? `Unable to update ${this.pageTitle.toLowerCase()}.`
      : `Unable to create ${this.pageTitle.toLowerCase()}.`;

    request$.subscribe({
      next: voucher => {
        if (isEdit) {
          this.setVoucher(voucher);
          this.toast.success(`${voucher.voucherNumber} updated successfully.`);
          this.saving = false;
          this.cdr.detectChanges();
          return;
        }

        this.toast.success(`${voucher.voucherNumber} created successfully.`);
        this.saving = false;
        this.resetForNextVoucher();
      },
      error: err => {
        this.toast.error(getApiErrorMessage(err, fallbackMessage));
        this.saving = false;
        this.cdr.detectChanges();
      }
    });
  }

  reset(): void {
    if (this.isEditMode) {
      this.loadData();
      return;
    }

    this.description = '';
    this.partyAccountId = null;
    this.voucherDate = new Date();
    this.lines = [this.newLine()];
    this.cashVoucherService.getNextNumber(this.mode).subscribe({
      next: voucherNumber => {
        this.voucherNumber = voucherNumber;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to refresh voucher number.');
        this.cdr.detectChanges();
      }
    });
  }

  private loadData(): void {
    this.loading = true;

    const idParam = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!idParam;
    this.voucherId = idParam ? Number(idParam) : null;

    const partyAccounts$ = this.mode === 'receipt'
      ? this.accountService.getCustomers()
      : this.accountService.getVendors();

    const cashAccounts$ = this.accountService.getJournalAccounts().pipe(
      map(accounts => accounts.filter(account => account.accountType === AccountType.Account))
    );

    const cashCategories$ = this.categoryService.getFiltered([AccountType.Account]);

    if (this.isEditMode && this.voucherId) {
      forkJoin({
        partyAccounts: partyAccounts$,
        cashAccounts: cashAccounts$,
        cashCategories: cashCategories$,
        voucher: this.cashVoucherService.getById(this.mode, this.voucherId)
      }).subscribe({
        next: ({ partyAccounts, cashAccounts, cashCategories, voucher }) => {
          this.setAccounts(partyAccounts, cashAccounts, cashCategories);
          this.setVoucher(voucher);
          this.loading = false;
          this.cdr.detectChanges();
        },
        error: () => {
          this.toast.error(`Unable to load ${this.pageTitle.toLowerCase()}.`);
          this.loading = false;
          this.cdr.detectChanges();
        }
      });
      return;
    }

    forkJoin({
      partyAccounts: partyAccounts$,
      cashAccounts: cashAccounts$,
      cashCategories: cashCategories$,
      voucherNumber: this.cashVoucherService.getNextNumber(this.mode)
    }).subscribe({
      next: ({ partyAccounts, cashAccounts, cashCategories, voucherNumber }) => {
        this.setAccounts(partyAccounts, cashAccounts, cashCategories);
        this.voucherNumber = voucherNumber;
        this.description = '';
        this.partyAccountId = null;
        this.voucherDate = new Date();
        this.lines = [this.newLine()];
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error(`Unable to initialize ${this.pageTitle.toLowerCase()}.`);
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  private setAccounts(partyAccounts: Account[], cashAccounts: Account[], cashCategories: Category[]): void {
    this.partyAccounts = partyAccounts;
    this.cashAccounts = cashAccounts;
    this.accountsById = new Map(cashAccounts.map(a => [a.id, a]));

    this.partyOptions = partyAccounts.map(account => ({
      value: account.id,
      label: account.name,
      sublabel: account.city || account.partyRole || 'Party'
    }));

    this.cashAccountOptions = cashAccounts.map(account => ({
      value: account.id,
      label: account.name,
      sublabel: account.bankName || 'Cash / Bank'
    }));

    this.cashCategoryOptions = cashCategories.map(category => ({
      value: category.id,
      label: category.name
    }));
  }

  getCashAccountOptionsForLine(line: EditableCashVoucherLine): SelectOption[] {
    if (!line.categoryId) return [];
    return this.cashAccounts
      .filter(account => account.categoryId === line.categoryId)
      .map(account => ({
        value: account.id,
        label: account.name,
        sublabel: account.bankName || 'Cash / Bank'
      }));
  }

  onLineCategoryChange(line: EditableCashVoucherLine, categoryId: number | null): void {
    line.categoryId = categoryId;
    line.accountId = null;
  }

  private setVoucher(voucher: CashVoucher): void {
    this.voucherId = voucher.id;
    this.isEditMode = true;
    this.voucherNumber = voucher.voucherNumber;
    this.voucherDate = new Date(voucher.date);
    this.description = voucher.description || '';
    this.partyAccountId = voucher.partyAccountId;
    this.lines = voucher.lines
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map(line => ({
        id: line.id,
        categoryId: this.accountsById.get(line.accountId)?.categoryId ?? null,
        accountId: line.accountId,
        description: line.description || '',
        amount: line.amount,
        isEdited: line.isEdited
      }));

    while (this.lines.length < 1) {
      this.lines.push(this.newLine());
    }
  }

  private buildRequest(): CreateCashVoucherRequest {
    return {
      date: toDateInputValue(this.voucherDate),
      partyAccountId: this.partyAccountId!,
      description: this.description.trim() || null,
      lines: this.lines.map((line, index) => ({
        accountId: line.accountId!,
        description: line.description.trim() || null,
        amount: round2(line.amount || 0),
        sortOrder: index
      }))
    };
  }

  private hasValidLines(): boolean {
    if (this.lines.length < 1) return false;

    return this.lines.every(line => {
      if (line.accountId == null) return false;
      return line.amount > 0;
    });
  }

  private sanitizeAmount(value: number): number {
    if (!Number.isFinite(value) || value < 0) return 0;
    return round2(value);
  }



  private newLine(): EditableCashVoucherLine {
    return {
      categoryId: null,
      accountId: null,
      description: '',
      amount: 0,
      isEdited: false
    };
  }

  private resetForNextVoucher(): void {
    this.description = '';
    this.partyAccountId = null;
    this.voucherDate = new Date();
    this.lines = [this.newLine()];
    this.cashVoucherService.getNextNumber(this.mode).subscribe({
      next: voucherNumber => {
        this.voucherNumber = voucherNumber;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Voucher created, but unable to fetch next number.');
        this.cdr.detectChanges();
      }
    });
  }

  private focusAccountSelector(index: number): void {
    setTimeout(() => {
      const accountSelect = this.accountSelects?.toArray()[index];
      accountSelect?.focusTrigger();
    });
  }
}
