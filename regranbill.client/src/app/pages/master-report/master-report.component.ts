import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { MasterReportService } from '../../services/master-report.service';
import { AccountService } from '../../services/account.service';
import { ToastService } from '../../services/toast.service';
import { MasterReport, MasterReportEntry } from '../../models/master-report.model';
import { Account } from '../../models/account.model';
import { forkJoin } from 'rxjs';

interface Category {
  id: number;
  name: string;
}

@Component({
  selector: 'app-master-report',
  templateUrl: './master-report.component.html',
  styleUrl: './master-report.component.css',
  standalone: false
})
export class MasterReportComponent implements OnInit {
  // Filters
  fromDate = '';
  toDate = '';
  selectedCategoryId: number | null = null;
  selectedAccountId: number | null = null;
  searchText = '';

  // Data
  categories: Category[] = [];
  accounts: Account[] = [];
  report: MasterReport | null = null;
  loading = false;
  filtersLoaded = false;

  constructor(
    private http: HttpClient,
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

  getFormattedDate(date: string): string {
    return new Date(date).toLocaleDateString('en-GB', {
      day: '2-digit',
      month: 'short',
      year: 'numeric'
    });
  }
}
