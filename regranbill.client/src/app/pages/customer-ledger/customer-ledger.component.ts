import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CustomerLedgerService } from '../../services/customer-ledger.service';
import { AccountService } from '../../services/account.service';
import { CategoryService } from '../../services/category.service';
import { ToastService } from '../../services/toast.service';
import { SearchableSelectComponent, SelectOption } from '../../components/searchable-select/searchable-select.component';
import { CustomerLedger, CustomerLedgerEntry } from '../../models/customer-ledger.model';
import { Account, AccountType, PartyRole } from '../../models/account.model';
import { Category } from '../../models/category.model';
import { formatDateDisplay, toDateInputValue } from '../../utils/date-utils';
import { getVoucherPrintPath, isPrintableVoucherType } from '../../utils/voucher-print-routes';

@Component({
  selector: 'app-customer-ledger',
  templateUrl: './customer-ledger.component.html',
  styleUrl: './customer-ledger.component.css',
  standalone: false
})
export class CustomerLedgerComponent implements OnInit {
  accounts: Account[] = [];
  categories: Category[] = [];
  categoryOptions: SelectOption[] = [];
  accountOptions: SelectOption[] = [];
  selectedCategoryId: number | null = null;
  selectedAccountId: number | null = null;
  fromDate: Date | null = null;
  toDate: Date | null = null;
  ledger: CustomerLedger | null = null;
  loading = false;

  constructor(
    private ledgerService: CustomerLedgerService,
    private accountService: AccountService,
    private categoryService: CategoryService,
    private cdr: ChangeDetectorRef,
    private toast: ToastService
  ) {}

  ngOnInit(): void {
    this.loadCategories();
  }

  loadCategories(): void {
    // Server-side filter: only categories with at least one Customer/Vendor/Both party account.
    this.categoryService.getFiltered(
      [AccountType.Party],
      [PartyRole.Customer, PartyRole.Vendor, PartyRole.Both]
    ).subscribe({
      next: (categories) => {
        const sorted = [...categories].sort((a, b) => a.name.localeCompare(b.name));
        this.categories = sorted;
        this.categoryOptions = sorted.map(c => ({ value: c.id, label: c.name }));
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load categories.');
      }
    });
  }

  loadAccountsByCategory(): void {
    if (!this.selectedCategoryId) {
      this.accounts = [];
      this.accountOptions = [];
      this.selectedAccountId = null;
      this.cdr.detectChanges();
      return;
    }

    this.accountService.getByCategory(this.selectedCategoryId).subscribe({
      next: (accounts) => {
        const filtered = accounts.filter(account =>
          account.accountType === AccountType.Party &&
          (account.partyRole === PartyRole.Customer
            || account.partyRole === PartyRole.Vendor
            || account.partyRole === PartyRole.Both)
        );
        const unique = new Map<number, Account>();
        filtered.forEach(account => unique.set(account.id, account));
        this.accounts = Array.from(unique.values()).sort((a, b) => a.name.localeCompare(b.name));
        this.accountOptions = this.accounts.map(account => ({
          value: account.id,
          label: `${account.name} (${this.getAccountSublabel(account)})`,
          sublabel: this.getAccountSublabel(account)
        }));
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load accounts.');
      }
    });
  }

  onCategoryChange(): void {
    this.ledger = null;
    this.selectedAccountId = null;
    this.loadAccountsByCategory();
  }

  loadLedger(): void {
    if (!this.selectedCategoryId) {
      this.toast.error('Please select a category.');
      return;
    }
    if (!this.selectedAccountId) {
      this.toast.error('Please select an account.');
      return;
    }
    this.loading = true;
    this.ledgerService.getLedger(
      this.selectedAccountId,
      toDateInputValue(this.fromDate),
      toDateInputValue(this.toDate)
    ).subscribe({
      next: data => {
        this.ledger = data;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load ledger.');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  clearFilters(): void {
    this.selectedCategoryId = null;
    this.selectedAccountId = null;
    this.accounts = [];
    this.accountOptions = [];
    this.fromDate = null;
    this.toDate = null;
    this.ledger = null;
    this.cdr.detectChanges();
  }

  getFormattedDate(date: string): string {
    return formatDateDisplay(date);
  }

  getVoucherTypeClass(voucherType: string): string {
    return `type-${voucherType.toLowerCase()}`;
  }

  isPrintableVoucher(entry: CustomerLedgerEntry): boolean {
    return isPrintableVoucherType(entry.voucherType, entry.voucherId, entry.voucherNumber);
  }

  openVoucherPrint(entry: CustomerLedgerEntry): void {
    const targetPath = getVoucherPrintPath(entry.voucherType, entry.voucherId, entry.voucherNumber);
    if (!targetPath) return;
    window.open(targetPath, '_blank');
  }

  printLedger(): void {
    if (!this.selectedAccountId) return;
    let target = `/print-customer-ledger/${this.selectedAccountId}`;
    const query = new URLSearchParams();
    const fromDateStr = toDateInputValue(this.fromDate);
    const toDateStr = toDateInputValue(this.toDate);
    if (fromDateStr) {
      query.set('fromDate', fromDateStr);
    }
    if (toDateStr) {
      query.set('toDate', toDateStr);
    }
    const queryString = query.toString();
    if (queryString) {
      target += `?${queryString}`;
    }
    window.open(target, '_blank');
  }

  private getAccountSublabel(account: Account): string {
    switch (account.partyRole) {
      case PartyRole.Customer: return 'Customer';
      case PartyRole.Vendor: return 'Vendor';
      case PartyRole.Transporter: return 'Transporter';
      case PartyRole.Both: return 'Customer & Vendor';
      default: return account.accountType;
    }
  }
}
