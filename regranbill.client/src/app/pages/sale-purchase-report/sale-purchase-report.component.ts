import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { Account } from '../../models/account.model';
import { SalePurchaseReport, SalePurchaseReportRow, SalePurchaseReportType } from '../../models/sale-purchase-report.model';
import { AccountService } from '../../services/account.service';
import { SalePurchaseReportService } from '../../services/sale-purchase-report.service';
import { ToastService } from '../../services/toast.service';
import { formatDateDisplay } from '../../utils/date-utils';

@Component({
  selector: 'app-sale-purchase-report',
  templateUrl: './sale-purchase-report.component.html',
  styleUrl: './sale-purchase-report.component.css',
  standalone: false
})
export class SalePurchaseReportComponent implements OnInit {
  fromDate = '';
  toDate = '';
  selectedType: SalePurchaseReportType = 'All';
  selectedProductId: number | null = null;
  searchText = '';

  products: Account[] = [];
  report: SalePurchaseReport | null = null;
  loading = false;
  filtersLoaded = false;

  constructor(
    private accountService: AccountService,
    private reportService: SalePurchaseReportService,
    private cdr: ChangeDetectorRef,
    private toast: ToastService
  ) {}

  ngOnInit(): void {
    this.accountService.getProducts().subscribe({
      next: products => {
        this.products = products;
        this.filtersLoaded = true;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load product filters.');
      }
    });
  }

  get filteredRows(): SalePurchaseReportRow[] {
    if (!this.report) return [];
    if (!this.searchText.trim()) return this.report.rows;

    const query = this.searchText.trim().toLowerCase();
    return this.report.rows.filter(row =>
      row.voucherNumber.toLowerCase().includes(query) ||
      row.productName.toLowerCase().includes(query) ||
      row.groupLabel.toLowerCase().includes(query) ||
      row.fromName.toLowerCase().includes(query) ||
      row.toName.toLowerCase().includes(query)
    );
  }

  isGroupStart(rows: SalePurchaseReportRow[], index: number): boolean {
    return index === 0 || rows[index - 1].groupLabel !== rows[index].groupLabel;
  }

  getGroupRowCount(groupLabel: string): number {
    return this.filteredRows.filter(row => row.groupLabel === groupLabel).length;
  }

  loadReport(): void {
    this.loading = true;
    this.reportService.getReport(
      this.fromDate || undefined,
      this.toDate || undefined,
      this.selectedType,
      this.selectedProductId ?? undefined
    ).subscribe({
      next: report => {
        this.report = report;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load sale purchase register.');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  clearFilters(): void {
    this.fromDate = '';
    this.toDate = '';
    this.selectedType = 'All';
    this.selectedProductId = null;
    this.searchText = '';
    this.loadReport();
  }

  getFormattedDate(date: string): string {
    return new Date(date).toLocaleDateString('en-GB', {
      day: '2-digit',
      month: 'short',
      year: 'numeric'
    });
  }

  isPrintableVoucher(row: SalePurchaseReportRow): boolean {
    return row.voucherType === 'SaleVoucher' || row.voucherType === 'PurchaseVoucher';
  }

  getVoucherLink(row: SalePurchaseReportRow): string[] {
    return row.voucherType === 'SaleVoucher'
      ? ['/print-dc', row.voucherId.toString()]
      : ['/print-pv', row.voucherId.toString()];
  }

  getTypeLabel(row: SalePurchaseReportRow): string {
    return row.voucherType === 'SaleVoucher' ? 'Sale' : 'Purchase';
  }

  getTypeClass(row: SalePurchaseReportRow): string {
    return row.voucherType === 'SaleVoucher' ? 'type-sale' : 'type-purchase';
  }
}
