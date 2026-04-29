import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { SearchableSelectComponent, SelectOption } from '../../components/searchable-select/searchable-select.component';
import { Account } from '../../models/account.model';
import { AccountClosingHistoryEntry, AccountClosingReport, AccountClosingSummaryRow } from '../../models/account-closing-report.model';
import { AccountService } from '../../services/account.service';
import { AccountClosingReportService } from '../../services/account-closing-report.service';
import { ToastService } from '../../services/toast.service';

@Component({
  selector: 'app-account-closing-report',
  templateUrl: './account-closing-report.component.html',
  styleUrl: './account-closing-report.component.css',
  standalone: false
})
export class AccountClosingReportComponent implements OnInit {
  fromDate = '';
  toDate = '';
  selectedFilterAccountId: number | null = null;
  selectedAccountId: number | null = null;
  searchText = '';

  accounts: Account[] = [];
  accountOptions: SelectOption[] = [];
  report: AccountClosingReport | null = null;
  selectedAccountHistory: AccountClosingHistoryEntry[] = [];
  loading = false;
  filtersLoaded = false;

  constructor(
    private router: Router,
    private accountService: AccountService,
    private reportService: AccountClosingReportService,
    private cdr: ChangeDetectorRef,
    private toast: ToastService
  ) {}

  ngOnInit(): void {
    this.accountService.getAll().subscribe({
      next: accounts => {
        const cashBankAccounts = accounts.filter(account => account.accountType === 'Account');
        this.accounts = cashBankAccounts;
        this.accountOptions = [
          { value: null as any, label: 'All Accounts' },
          ...cashBankAccounts.map(a => ({
            value: a.id,
            label: a.name,
            sublabel: a.bankName || 'Cash / Bank'
          }))
        ];
        this.filtersLoaded = true;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load account filters.');
      }
    });
  }

  get filteredSummaryRows(): AccountClosingSummaryRow[] {
    if (!this.report) return [];
    if (!this.searchText.trim()) return this.report.accounts;

    const query = this.searchText.trim().toLowerCase();
    return this.report.accounts.filter(row => row.accountName.toLowerCase().includes(query));
  }

  get selectedSummaryRow(): AccountClosingSummaryRow | null {
    if (!this.report || !this.selectedAccountId) return null;
    return this.report.accounts.find(row => row.accountId === this.selectedAccountId) ?? null;
  }

  get historyRows(): AccountClosingHistoryEntry[] {
    return this.selectedAccountHistory;
  }

  loadReport(): void {
    this.loading = true;
    const historyTargetId = this.selectedFilterAccountId ?? this.selectedAccountId ?? undefined;
    this.reportService.getReport(
      this.fromDate || undefined,
      this.toDate || undefined,
      this.selectedFilterAccountId ?? undefined,
      historyTargetId
    ).subscribe({
      next: report => {
        this.report = report;
        if (historyTargetId && report.history?.length) {
          this.selectedAccountId = historyTargetId;
          this.selectedAccountHistory = report.history ?? [];
        } else {
          this.selectedAccountHistory = [];
          if (this.selectedAccountId && !report.accounts.some(row => row.accountId === this.selectedAccountId)) {
            this.selectedAccountId = null;
          }
        }
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load account closing report.');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  clearFilters(): void {
    this.fromDate = '';
    this.toDate = '';
    this.selectedFilterAccountId = null;
    this.selectedAccountId = null;
    this.selectedAccountHistory = [];
    this.searchText = '';
    this.loadReport();
  }

  selectAccount(row: AccountClosingSummaryRow): void {
    this.selectedAccountId = row.accountId;
    this.loading = true;
    this.reportService.getReport(
      this.fromDate || undefined,
      this.toDate || undefined,
      this.selectedFilterAccountId ?? undefined,
      row.accountId
    ).subscribe({
      next: report => {
        this.report = report;
        this.selectedAccountHistory = report.history ?? [];
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load account history.');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  closeHistory(): void {
    this.selectedAccountId = null;
    this.selectedAccountHistory = [];
    this.cdr.detectChanges();
  }

  isSelectedRow(accountId: number): boolean {
    return this.selectedAccountId === accountId;
  }

  getFormattedDate(date: string): string {
    return new Date(date).toLocaleDateString('en-GB', {
      day: '2-digit',
      month: 'short',
      year: 'numeric'
    });
  }

  isPrintableVoucher(entry: AccountClosingHistoryEntry): boolean {
    return (entry.voucherType === 'SaleVoucher' || entry.voucherType === 'PurchaseVoucher') && entry.voucherId > 0;
  }

  getVoucherPrintLink(entry: AccountClosingHistoryEntry): string[] | null {
    if (!this.isPrintableVoucher(entry)) return null;
    return entry.voucherType === 'SaleVoucher'
      ? ['/print-dc', entry.voucherId.toString()]
      : ['/print-pv', entry.voucherId.toString()];
  }

  openPrint(): void {
    const queryParams: Record<string, string> = {};
    if (this.fromDate) queryParams['from'] = this.fromDate;
    if (this.toDate) queryParams['to'] = this.toDate;
    if (this.selectedFilterAccountId !== null) queryParams['accountId'] = this.selectedFilterAccountId.toString();
    if (this.selectedAccountId !== null) queryParams['historyAccountId'] = this.selectedAccountId.toString();

    const url = this.router.serializeUrl(this.router.createUrlTree(['/print-account-closing-report'], { queryParams }));
    window.open(url, '_blank');
  }
}
