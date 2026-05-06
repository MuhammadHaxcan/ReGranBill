import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { forkJoin } from 'rxjs';
import { StatementService } from '../../services/statement.service';
import { AccountService } from '../../services/account.service';
import { ToastService } from '../../services/toast.service';
import { SearchableSelectComponent, SelectOption } from '../../components/searchable-select/searchable-select.component';
import { StatementEntry, StatementOfAccount } from '../../models/statement.model';
import { Account, PartyRole } from '../../models/account.model';
import { formatDateDisplay, toDateInputValue } from '../../utils/date-utils';

@Component({
  selector: 'app-soa',
  templateUrl: './soa.component.html',
  styleUrl: './soa.component.css',
  standalone: false
})
export class SoaComponent implements OnInit {
  customers: Account[] = [];
  accountOptions: SelectOption[] = [];
  selectedAccountId: number | null = null;
  fromDate: Date | null = null;
  toDate: Date | null = null;
  statement: StatementOfAccount | null = null;
  loading = false;

  constructor(
    private route: ActivatedRoute,
    private statementService: StatementService,
    private accountService: AccountService,
    private cdr: ChangeDetectorRef,
    private toast: ToastService
  ) {}

  ngOnInit(): void {
    forkJoin({
      customers: this.accountService.getCustomers(),
      vendors: this.accountService.getVendors(),
      transporters: this.accountService.getTransporters()
    }).subscribe({
      next: ({ customers, vendors, transporters }) => {
        const unique = new Map<number, Account>();
        [...customers, ...vendors, ...transporters].forEach(account => unique.set(account.id, account));
        this.customers = Array.from(unique.values()).sort((a, b) => a.name.localeCompare(b.name));
        this.accountOptions = this.customers.map(account => ({
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

    const paramId = this.route.snapshot.paramMap.get('accountId');
    if (paramId) {
      this.selectedAccountId = +paramId;
      this.loadStatement();
    }
  }

  loadStatement(): void {
    if (!this.selectedAccountId) return;
    this.loading = true;
    this.statementService.getStatement(
      this.selectedAccountId,
      toDateInputValue(this.fromDate) || undefined,
      toDateInputValue(this.toDate) || undefined
    ).subscribe({
      next: data => {
        this.statement = data;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load statement.');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  clearFilters(): void {
    this.fromDate = null;
    this.toDate = null;
    if (this.selectedAccountId) {
      this.loadStatement();
    }
  }

  get canPrintStatement(): boolean {
    const role = this.statement?.partyRole;
    return !!this.statement && (role === PartyRole.Customer || role === PartyRole.Both);
  }

  getFormattedDate(date: string): string {
    return formatDateDisplay(date);
  }

  isPrintableVoucher(entry: StatementEntry): boolean {
    return (entry.voucherType === 'SaleVoucher' || entry.voucherType === 'PurchaseVoucher') && entry.voucherId > 0;
  }

  openVoucherPrint(entry: StatementEntry): void {
    if (!this.isPrintableVoucher(entry)) return;

    const targetPath = entry.voucherType === 'SaleVoucher'
      ? `/print-dc/${entry.voucherId}`
      : `/print-pv/${entry.voucherId}`;

    window.open(targetPath, '_blank');
  }

  printStatement(): void {
    if (!this.selectedAccountId || !this.canPrintStatement) return;

    const params = new URLSearchParams();
    const fromDateStr = toDateInputValue(this.fromDate);
    const toDateStr = toDateInputValue(this.toDate);
    if (fromDateStr) params.set('fromDate', fromDateStr);
    if (toDateStr) params.set('toDate', toDateStr);

    const query = params.toString();
    const target = query
      ? `/print-soa/${this.selectedAccountId}?${query}`
      : `/print-soa/${this.selectedAccountId}`;

    window.open(target, '_blank');
  }

  private getAccountSublabel(account: Account): string {
    if (account.accountType === 'Party') {
      switch (account.partyRole) {
        case PartyRole.Customer: return 'Customer';
        case PartyRole.Vendor: return 'Vendor';
        case PartyRole.Transporter: return 'Transporter';
        case PartyRole.Both: return 'Customer & Vendor';
      }
    }
    if (account.accountType === 'Account') return account.bankName || 'Cash / Bank';
    if (account.accountType === 'Product') return account.packing || 'Product';
    if (account.accountType === 'RawMaterial') return account.packing || 'Raw Material';
    if (account.accountType === 'Expense') return 'Expense';
    return account.accountType;
  }
}
