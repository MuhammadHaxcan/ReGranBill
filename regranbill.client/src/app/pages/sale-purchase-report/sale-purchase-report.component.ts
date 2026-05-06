import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { SearchableSelectComponent, SelectOption } from '../../components/searchable-select/searchable-select.component';
import { Account } from '../../models/account.model';
import { SalePurchaseReport, SalePurchaseReportRow, SalePurchaseReportType } from '../../models/sale-purchase-report.model';
import { AccountService } from '../../services/account.service';
import { SalePurchaseReportService } from '../../services/sale-purchase-report.service';
import { ToastService } from '../../services/toast.service';
import { toDateInputValue } from '../../utils/date-utils';

@Component({
  selector: 'app-sale-purchase-report',
  templateUrl: './sale-purchase-report.component.html',
  styleUrl: './sale-purchase-report.component.css',
  standalone: false
})
export class SalePurchaseReportComponent implements OnInit {
  fromDate: Date | null = null;
  toDate: Date | null = null;
  selectedType: SalePurchaseReportType = 'All';
  selectedProductId: number | null = null;
  searchText = '';

  readonly typeOptions: SelectOption[] = [
    { value: 'All', label: 'All' },
    { value: 'Sale', label: 'Sale' },
    { value: 'Purchase', label: 'Purchase' }
  ];

  products: Account[] = [];
  productOptions: SelectOption[] = [];
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
        this.productOptions = [
          { value: null as any, label: 'All Products' },
          ...products.map(p => ({
            value: p.id,
            label: p.name,
            sublabel: p.packing || ''
          }))
        ];
        this.filtersLoaded = true;
        
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
      toDateInputValue(this.fromDate) || undefined,
      toDateInputValue(this.toDate) || undefined,
      this.selectedType,
      this.selectedProductId ?? undefined
    ).subscribe({
      next: report => {
        this.report = report;
        this.loading = false;
        
      },
      error: () => {
        this.toast.error('Unable to load sale purchase register.');
        this.loading = false;
        
      }
    });
  }

  onTypeChange(value: SalePurchaseReportType | null): void {
    this.selectedType = value ?? 'All';
  }

  clearFilters(): void {
    this.fromDate = null;
    this.toDate = null;
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
    return row.voucherType === 'SaleVoucher' || row.voucherType === 'SaleReturnVoucher' || row.voucherType === 'PurchaseVoucher' || row.voucherType === 'PurchaseReturnVoucher';
  }

  getVoucherLink(row: SalePurchaseReportRow): string[] {
    if (row.voucherType === 'SaleVoucher') return ['/print-dc', row.voucherId.toString()];
    if (row.voucherType === 'SaleReturnVoucher') return ['/print-sr', row.voucherId.toString()];
    if (row.voucherType === 'PurchaseReturnVoucher') return ['/print-pr', row.voucherId.toString()];
    return ['/print-pv', row.voucherId.toString()];
  }

  getTypeLabel(row: SalePurchaseReportRow): string {
    if (row.voucherType === 'SaleVoucher') return 'Sale';
    if (row.voucherType === 'SaleReturnVoucher') return 'Sale Return';
    if (row.voucherType === 'PurchaseReturnVoucher') return 'Purchase Return';
    return 'Purchase';
  }

  getTypeClass(row: SalePurchaseReportRow): string {
    if (row.voucherType === 'SaleVoucher') return 'type-sale';
    if (row.voucherType === 'SaleReturnVoucher') return 'type-sale-return';
    if (row.voucherType === 'PurchaseReturnVoucher') return 'type-purchase-return';
    return 'type-purchase';
  }
}
