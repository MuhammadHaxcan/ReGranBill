import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { MasterReportService } from '../../services/master-report.service';
import { AccountService } from '../../services/account.service';
import { ToastService } from '../../services/toast.service';
import { MasterReport, MasterReportEntry } from '../../models/master-report.model';
import { formatDateDisplay } from '../../utils/date-utils';
import { Account } from '../../models/account.model';
import { forkJoin } from 'rxjs';

interface Category {
  id: number;
  name: string;
}

type MasterReportColumnKey = 'voucher' | 'date' | 'description' | 'account' | 'quantity' | 'rate' | 'debit' | 'credit' | 'balance';

interface MasterReportColumnOption {
  key: MasterReportColumnKey;
  label: string;
}

@Component({
  selector: 'app-master-report',
  templateUrl: './master-report.component.html',
  styleUrl: './master-report.component.css',
  standalone: false
})
export class MasterReportComponent implements OnInit {
  readonly columnOptions: MasterReportColumnOption[] = [
    { key: 'voucher', label: 'Voucher' },
    { key: 'date', label: 'Date' },
    { key: 'description', label: 'Description' },
    { key: 'account', label: 'Account' },
    { key: 'quantity', label: 'Quantity' },
    { key: 'rate', label: 'Rate' },
    { key: 'debit', label: 'Debit' },
    { key: 'credit', label: 'Credit' },
    { key: 'balance', label: 'Balance' }
  ];

  // Filters
  fromDate = '';
  toDate = '';
  selectedCategoryId: number | null = null;
  selectedAccountId: number | null = null;
  searchText = '';
  visibleColumnKeys: MasterReportColumnKey[] = this.columnOptions.map(column => column.key);
  showColumnPanel = false;

  // Data
  categories: Category[] = [];
  accounts: Account[] = [];
  report: MasterReport | null = null;
  loading = false;
  filtersLoaded = false;

  constructor(
    private http: HttpClient,
    private router: Router,
    private reportService: MasterReportService,
    private accountService: AccountService,
    private cdr: ChangeDetectorRef,
    private toast: ToastService
  ) {}

  ngOnInit(): void {
    this.loadFilters();
  }

  private loadFilters(): void {
    forkJoin({
      accounts: this.accountService.getAll(),
      categories: this.http.get<Category[]>('/api/categories')
    }).subscribe({
      next: ({ accounts, categories }) => {
        this.accounts = accounts;
        this.categories = categories;
        this.filtersLoaded = true;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load filters.');
      }
    });
  }

  get filteredAccounts(): Account[] {
    if (!this.selectedCategoryId) return this.accounts;
    return this.accounts.filter(a => a.categoryId === this.selectedCategoryId);
  }

  get filteredEntries(): MasterReportEntry[] {
    if (!this.report) return [];
    if (!this.searchText.trim()) return this.report.entries;
    const q = this.searchText.toLowerCase();
    return this.report.entries.filter(e =>
      e.accountName.toLowerCase().includes(q) ||
      e.description?.toLowerCase().includes(q) ||
      e.voucherNumber.toLowerCase().includes(q)
    );
  }

  get visibleColumns(): MasterReportColumnOption[] {
    return this.columnOptions.filter(column => this.visibleColumnKeys.includes(column.key));
  }

  get totalColumns(): MasterReportColumnKey[] {
    return this.visibleColumnKeys.filter(column => column === 'debit' || column === 'credit' || column === 'balance');
  }

  get totalsLabelSpan(): number {
    return this.visibleColumnKeys.length - this.totalColumns.length;
  }

  loadReport(): void {
    this.loading = true;
    this.reportService.getReport(
      this.fromDate || undefined,
      this.toDate || undefined,
      this.selectedCategoryId ?? undefined,
      this.selectedAccountId ?? undefined
    ).subscribe({
      next: data => {
        this.report = data;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load report.');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  clearFilters(): void {
    this.fromDate = '';
    this.toDate = '';
    this.selectedCategoryId = null;
    this.selectedAccountId = null;
    this.searchText = '';
    this.loadReport();
  }

  onCategoryChange(): void {
    this.selectedAccountId = null;
  }

  isColumnVisible(column: MasterReportColumnKey): boolean {
    return this.visibleColumnKeys.includes(column);
  }

  toggleColumn(column: MasterReportColumnKey): void {
    if (this.isColumnVisible(column)) {
      if (this.visibleColumnKeys.length === 1) {
        this.toast.info('At least one column must remain visible.');
        return;
      }

      this.visibleColumnKeys = this.visibleColumnKeys.filter(key => key !== column);
      return;
    }

    this.visibleColumnKeys = this.columnOptions
      .map(option => option.key)
      .filter(key => key === column || this.visibleColumnKeys.includes(key));
  }

  getFormattedDate(date: string): string {
    return new Date(date).toLocaleDateString('en-GB', {
      day: '2-digit',
      month: 'short',
      year: 'numeric'
    });
  }

  openPrint(): void {
    const queryParams: Record<string, string> = {};
    if (this.fromDate) queryParams['from'] = this.fromDate;
    if (this.toDate) queryParams['to'] = this.toDate;
    if (this.selectedCategoryId !== null) queryParams['categoryId'] = this.selectedCategoryId.toString();
    if (this.selectedAccountId !== null) queryParams['accountId'] = this.selectedAccountId.toString();
    queryParams['columns'] = this.visibleColumnKeys.join(',');

    const url = this.router.serializeUrl(this.router.createUrlTree(['/print-master-report'], { queryParams }));
    window.open(url, '_blank');
  }
}
