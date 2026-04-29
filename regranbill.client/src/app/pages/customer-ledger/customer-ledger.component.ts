import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { forkJoin } from 'rxjs';
import { CustomerLedgerService } from '../../services/customer-ledger.service';
import { AccountService } from '../../services/account.service';
import { ToastService } from '../../services/toast.service';
import { SearchableSelectComponent, SelectOption } from '../../components/searchable-select/searchable-select.component';
import { CustomerLedger, CustomerLedgerEntry } from '../../models/customer-ledger.model';
import { Account, PartyRole } from '../../models/account.model';
import { formatDateDisplay } from '../../utils/date-utils';

@Component({
  selector: 'app-customer-ledger',
  templateUrl: './customer-ledger.component.html',
  styleUrl: './customer-ledger.component.css',
  standalone: false
})
export class CustomerLedgerComponent implements OnInit {
  accounts: Account[] = [];
  accountOptions: SelectOption[] = [];
  selectedAccountId: number | null = null;
  fromDate = '';
  toDate = '';
  ledger: CustomerLedger | null = null;
  loading = false;
  activeTab: 'customer' | 'vendor' = 'customer';

  constructor(
    private ledgerService: CustomerLedgerService,
    private accountService: AccountService,
    private cdr: ChangeDetectorRef,
    private toast: ToastService
  ) {}

  ngOnInit(): void {
    this.loadAccounts();
  }

  loadAccounts(): void {
    forkJoin({
      customers: this.accountService.getCustomers(),
      vendors: this.accountService.getVendors()
    }).subscribe({
      next: ({ customers, vendors }) => {
        const unique = new Map<number, Account>();
        const targetType = this.activeTab === 'customer' ? PartyRole.Customer : PartyRole.Vendor;
        const sourceList = this.activeTab === 'customer' ? customers : vendors;
        sourceList.forEach(account => unique.set(account.id, account));
        this.accounts = Array.from(unique.values()).sort((a, b) => a.name.localeCompare(b.name));
        this.accountOptions = this.accounts.map(account => ({
          value: account.id,
          label: account.name,
          sublabel: this.getAccountSublabel(account)
        }));
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load accounts.');
      }
    });
  }

  switchTab(tab: 'customer' | 'vendor'): void {
    this.activeTab = tab;
    this.ledger = null;
    this.selectedAccountId = null;
    this.loadAccounts();
  }

  loadLedger(): void {
    if (!this.selectedAccountId || !this.fromDate || !this.toDate) {
      this.toast.error('Please select an account and date range.');
      return;
    }
    this.loading = true;
    this.ledgerService.getLedger(
      this.selectedAccountId,
      this.fromDate,
      this.toDate
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
    this.fromDate = '';
    this.toDate = '';
    this.ledger = null;
  }

  getFormattedDate(date: string): string {
    return formatDateDisplay(date);
  }

  getVoucherTypeClass(voucherType: string): string {
    return `type-${voucherType.toLowerCase()}`;
  }

  isPrintableVoucher(entry: CustomerLedgerEntry): boolean {
    return (entry.voucherType === 'SaleVoucher' || entry.voucherType === 'PurchaseVoucher') && entry.voucherId > 0;
  }

  openVoucherPrint(entry: CustomerLedgerEntry): void {
    if (!this.isPrintableVoucher(entry)) return;
    const targetPath = entry.voucherType === 'SaleVoucher'
      ? `/print-dc/${entry.voucherId}`
      : `/print-pv/${entry.voucherId}`;
    window.open(targetPath, '_blank');
  }

  printLedger(): void {
    if (!this.selectedAccountId || !this.fromDate || !this.toDate) return;
    const target = `/print-customer-ledger/${this.selectedAccountId}?fromDate=${this.fromDate}&toDate=${this.toDate}`;
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