import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { forkJoin } from 'rxjs';
import { Account, AccountType } from '../../models/account.model';
import { RawMaterialLotMovement, RawMaterialLotReport, RawMaterialLotRow } from '../../models/raw-material-lot-report.model';
import { SearchableSelectComponent, SelectOption } from '../../components/searchable-select/searchable-select.component';
import { AccountService } from '../../services/account.service';
import { RawMaterialLotReportService } from '../../services/raw-material-lot-report.service';
import { ToastService } from '../../services/toast.service';
import { toDateInputValue } from '../../utils/date-utils';

@Component({
  selector: 'app-raw-material-lot-report',
  templateUrl: './raw-material-lot-report.component.html',
  styleUrl: './raw-material-lot-report.component.css',
  standalone: false
})
export class RawMaterialLotReportComponent implements OnInit {
  fromDate: Date | null = null;
  toDate: Date | null = null;
  selectedVendorId: number | null = null;
  selectedProductId: number | null = null;
  lotNumber = '';
  openOnly = false;
  report: RawMaterialLotReport | null = null;
  loading = false;
  filtersLoaded = false;
  selectedLotId: number | null = null;
  filtersExpanded = true;

  vendorOptions: SelectOption[] = [];
  productOptions: SelectOption[] = [];

  constructor(
    private accountService: AccountService,
    private reportService: RawMaterialLotReportService,
    private toast: ToastService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadFilters();
  }

  private loadFilters(): void {
    forkJoin({
      accounts: this.accountService.getAll()
    }).subscribe({
      next: ({ accounts }) => {
        const vendors = accounts.filter(a => a.accountType === AccountType.Party);
        const products = accounts.filter(a => a.accountType === AccountType.RawMaterial || a.accountType === AccountType.UnwashedMaterial);
        this.vendorOptions = [{ value: null as any, label: 'All Vendors' }, ...vendors.map(v => ({ value: v.id, label: v.name }))];
        this.productOptions = [{ value: null as any, label: 'All Materials' }, ...products.map(p => ({ value: p.id, label: p.name }))];
        this.filtersLoaded = true;
        this.loadReport();
      },
      error: () => this.toast.error('Unable to load lot report filters.')
    });
  }

  loadReport(): void {
    this.loading = true;
    this.selectedLotId = null;
    this.reportService.getReport({
      from: toDateInputValue(this.fromDate) || undefined,
      to: toDateInputValue(this.toDate) || undefined,
      vendorId: this.selectedVendorId ?? undefined,
      productId: this.selectedProductId ?? undefined,
      lotNumber: this.lotNumber.trim() || undefined,
      openOnly: this.openOnly,
      includeDetails: true
    }).subscribe({
      next: report => {
        this.report = report;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load raw material lot report.');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  clearFilters(): void {
    this.fromDate = null;
    this.toDate = null;
    this.selectedVendorId = null;
    this.selectedProductId = null;
    this.lotNumber = '';
    this.openOnly = false;
    this.selectedLotId = null;
    this.loadReport();
  }

  toggleFilters(): void {
    this.filtersExpanded = !this.filtersExpanded;
  }

  selectLot(lotId: number): void {
    this.selectedLotId = lotId;
  }

  get selectedLot(): RawMaterialLotRow | null {
    return this.report?.lots.find(lot => lot.lotId === this.selectedLotId) ?? null;
  }

  get selectedMovements(): RawMaterialLotMovement[] {
    if (!this.report || !this.selectedLotId) return [];
    return this.report.movements.filter(m => m.lotId === this.selectedLotId);
  }

  get totalOriginalKg(): number {
    return this.report?.lots.reduce((sum, lot) => sum + lot.originalWeightKg, 0) ?? 0;
  }

  get totalAvailableKg(): number {
    return this.report?.lots.reduce((sum, lot) => sum + lot.availableWeightKg, 0) ?? 0;
  }

  get totalConsumedKg(): number {
    return this.report?.lots.reduce((sum, lot) => sum + lot.consumedWeightKg, 0) ?? 0;
  }

  get openLotCount(): number {
    return this.report?.lots.filter(lot => lot.status === 'Open').length ?? 0;
  }

  formatDate(value: string): string {
    return new Date(value).toLocaleDateString('en-GB', {
      day: '2-digit',
      month: 'short',
      year: 'numeric'
    });
  }
}
