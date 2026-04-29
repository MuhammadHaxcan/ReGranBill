import { ChangeDetectorRef, Component, HostListener, OnInit, QueryList, ViewChildren } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { forkJoin, Observable } from 'rxjs';
import { SearchableSelectComponent, SelectOption } from '../../components/searchable-select/searchable-select.component';
import { Account } from '../../models/account.model';
import { Category } from '../../models/category.model';
import { CreateJournalVoucherRequest, JournalVoucher } from '../../models/journal-voucher.model';
import { AccountService } from '../../services/account.service';
import { CategoryService } from '../../services/category.service';
import { JournalVoucherService } from '../../services/journal-voucher.service';
import { ToastService } from '../../services/toast.service';
import { round2 } from '../../utils/delivery-calculations';

interface EditableJournalLine {
  id?: number;
  categoryId: number | null;
  accountId: number | null;
  description: string;
  debit: number;
  credit: number;
  isEdited: boolean;
}

@Component({
  selector: 'app-journal-voucher',
  templateUrl: './journal-voucher.component.html',
  styleUrl: './journal-voucher.component.css',
  standalone: false
})
export class JournalVoucherComponent implements OnInit {
  @ViewChildren('accountSelect') accountSelects!: QueryList<SearchableSelectComponent>;

  voucherId: number | null = null;
  isEditMode = false;
  loading = true;
  saving = false;

  voucherNumber = '';
  voucherDate = new Date();
  description = '';

  categories: Category[] = [];
  categoryOptions: SelectOption[] = [];
  // per-category account cache, keyed by categoryId
  accountsByCategory = new Map<number, Account[]>();
  // all accounts loaded once (used in edit mode to resolve categoryId from accountId)
  allAccounts: Account[] = [];
  lines: EditableJournalLine[] = [];

  // track in-flight API calls per categoryId to avoid duplicate requests
  private categoryAccountRequests = new Map<number, Observable<Account[]>>();

  constructor(
    private route: ActivatedRoute,
    private cdr: ChangeDetectorRef,
    private accountService: AccountService,
    private categoryService: CategoryService,
    private journalVoucherService: JournalVoucherService,
    private toast: ToastService
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
    return !this.loading && !this.saving && this.isBalanced && this.hasValidLines();
  }

  ngOnInit(): void {
    this.loadData();
  }

  @HostListener('document:keydown', ['$event'])
  handleKeyboardShortcut(event: KeyboardEvent): void {
    if (this.loading || this.saving) return;
    if (event.altKey && event.key.toLowerCase() === 'n') {
      event.preventDefault();
      this.addLine(true);
    }
  }

  private loadData(): void {
    this.loading = true;

    const idParam = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!idParam;
    this.voucherId = idParam ? Number(idParam) : null;

    if (this.isEditMode && this.voucherId) {
      forkJoin({
        allAccounts: this.accountService.getAll(),
        categories: this.categoryService.getAll(),
        voucher: this.journalVoucherService.getById(this.voucherId)
      }).subscribe({
        next: ({ allAccounts, categories, voucher }) => {
          this.allAccounts = allAccounts;
          this.setCategories(categories);
          this.setVoucher(voucher);
          this.loading = false;
          this.cdr.detectChanges();
        },
        error: () => {
          this.toast.error('Unable to load journal voucher.');
          this.loading = false;
          this.cdr.detectChanges();
        }
      });
      return;
    }

    forkJoin({
      categories: this.categoryService.getAll(),
      voucherNumber: this.journalVoucherService.getNextNumber()
    }).subscribe({
      next: ({ categories, voucherNumber }) => {
        this.setCategories(categories);
        this.voucherNumber = voucherNumber;
        this.description = '';
        this.voucherDate = new Date();
        this.lines = [this.newLine(), this.newLine()];
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to initialize journal voucher.');
        this.loading = false;
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

  onAccountChange(line: EditableJournalLine, accountId: number): void {
    line.accountId = accountId;
  }

  onDebitChange(line: EditableJournalLine): void {
    line.debit = this.sanitizeAmount(line.debit);
    if (line.debit > 0) line.credit = 0;
  }

  onCreditChange(line: EditableJournalLine): void {
    line.credit = this.sanitizeAmount(line.credit);
    if (line.credit > 0) line.debit = 0;
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

  save(): void {
    if (!this.canSave) return;

    const request = this.buildRequest();
    this.saving = true;

    if (this.isEditMode && this.voucherId) {
      this.journalVoucherService.update(this.voucherId, request).subscribe({
        next: voucher => {
          this.setVoucher(voucher);
          this.toast.success(`${voucher.voucherNumber} updated successfully.`);
          this.saving = false;
          this.cdr.detectChanges();
        },
        error: err => {
          this.toast.error(err?.error?.message || 'Unable to update journal voucher.');
          this.saving = false;
          this.cdr.detectChanges();
        }
      });
      return;
    }

    this.journalVoucherService.create(request).subscribe({
      next: (voucher: any) => {
        this.saving = false;
        const num = voucher?.voucherNumber || this.voucherNumber;
        this.toast.success(`${num} created successfully.`);
        this.resetForNextVoucher();
      },
      error: err => {
        this.toast.error(err?.error?.message || 'Unable to create journal voucher.');
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
    this.voucherDate = new Date();
    this.lines = [this.newLine(), this.newLine()];
    this.journalVoucherService.getNextNumber().subscribe({
      next: num => {
        this.voucherNumber = num;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to refresh voucher number.');
        this.cdr.detectChanges();
      }
    });
  }

  private buildRequest(): CreateJournalVoucherRequest {
    return {
      date: this.voucherDate,
      description: this.description.trim() || null,
      entries: this.lines.map((line, index) => ({
        accountId: line.accountId!,
        description: line.description.trim() || null,
        debit: round2(line.debit || 0),
        credit: round2(line.credit || 0),
        sortOrder: index
      }))
    };
  }

  private hasValidLines(): boolean {
    if (this.lines.length < 2) return false;

    return this.lines.every(line => {
      if (line.categoryId == null) return false;
      if (line.accountId == null) return false;
      if (line.debit < 0 || line.credit < 0) return false;

      const hasDebit = (line.debit || 0) > 0;
      const hasCredit = (line.credit || 0) > 0;
      return hasDebit !== hasCredit;
    });
  }

  private setCategories(categories: Category[]): void {
    this.categories = categories;
    this.categoryOptions = categories.map(c => ({ value: c.id, label: c.name }));
  }

  getAccountOptionsForLine(line: EditableJournalLine): SelectOption[] {
    if (line.categoryId == null) return [];
    const accounts = this.accountsByCategory.get(line.categoryId);
    if (!accounts) return [];
    return accounts.map(account => ({
      value: account.id,
      label: account.name,
      sublabel: this.getAccountSublabel(account)
    }));
  }

  onCategoryChange(line: EditableJournalLine, categoryId: number): void {
    line.categoryId = categoryId;
    line.accountId = null;
    this.loadAccountsForCategory(categoryId);
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

  private setVoucher(voucher: JournalVoucher): void {
    this.voucherId = voucher.id;
    this.isEditMode = true;
    this.voucherNumber = voucher.voucherNumber;
    this.voucherDate = new Date(voucher.date);
    this.description = voucher.description || '';
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
          isEdited: entry.isEdited
        };
      });

    while (this.lines.length < 2) {
      this.lines.push(this.newLine());
    }
  }

  private sanitizeAmount(value: number): number {
    if (!Number.isFinite(value) || value < 0) return 0;
    return round2(value);
  }



  private newLine(): EditableJournalLine {
    return {
      categoryId: null,
      accountId: null,
      description: '',
      debit: 0,
      credit: 0,
      isEdited: false
    };
  }

  private resetForNextVoucher(): void {
    this.description = '';
    this.voucherDate = new Date();
    this.lines = [this.newLine(), this.newLine()];
    this.journalVoucherService.getNextNumber().subscribe({
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

  private getAccountSublabel(account: Account): string {
    if (account.accountType === 'Expense') return 'Expense';
    if (account.accountType === 'Account') return account.bankName || 'Cash / Bank';
    if (account.accountType === 'Party') return account.partyRole || 'Party';
    return account.accountType;
  }
}
