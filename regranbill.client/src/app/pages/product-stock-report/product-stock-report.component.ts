import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { formatDateDisplay, toDateInputValue } from '../../utils/date-utils';
import { SearchableSelectComponent, SelectOption } from '../../components/searchable-select/searchable-select.component';
import { Account } from '../../models/account.model';
import { Category } from '../../models/category.model';
import {
  ProductStockMovement,
  ProductStockReport,
  ProductStockRow
} from '../../models/product-stock-report.model';
import { AccountService } from '../../services/account.service';
import { CategoryService } from '../../services/category.service';
import { ProductStockReportService } from '../../services/product-stock-report.service';
import { ToastService } from '../../services/toast.service';

@Component({
  selector: 'app-product-stock-report',
  templateUrl: './product-stock-report.component.html',
  styleUrl: './product-stock-report.component.css',
  standalone: false
})
export class ProductStockReportComponent implements OnInit {
  fromDate: Date | null = null;
  toDate: Date | null = null;
  searchText = '';
  selectedCategoryId: number | null = null;
  selectedProductId: number | null = null;
  selectedRowProductId: number | null = null;

  categories: Category[] = [];
  products: Account[] = [];
  categoryOptions: SelectOption[] = [];
  productOptions: SelectOption[] = [];
  report: ProductStockReport | null = null;
  loading = false;
  filtersLoaded = false;

  constructor(
    private router: Router,
    private accountService: AccountService,
    private categoryService: CategoryService,
    private reportService: ProductStockReportService,
    private cdr: ChangeDetectorRef,
    private toast: ToastService
  ) {}

  ngOnInit(): void {
    this.loadFilters();
  }

  private loadFilters(): void {
    forkJoin({
      categories: this.categoryService.getAll(),
      products: this.accountService.getProducts()
    }).subscribe({
      next: ({ categories, products }) => {
        this.categories = categories;
        this.products = products;
        this.categoryOptions = [
          { value: null as any, label: 'All Categories' },
          ...categories.map(c => ({ value: c.id, label: c.name }))
        ];
        this.productOptions = this.buildProductOptions();
        this.filtersLoaded = true;
        this.cdr.detectChanges();
        this.loadReport();
      },
      error: () => {
        this.toast.error('Unable to load product stock filters.');
      }
    });
  }

  get filteredProducts(): Account[] {
    if (!this.selectedCategoryId) return this.products;
    return this.products.filter(p => p.categoryId === this.selectedCategoryId);
  }

  get filteredRows(): ProductStockRow[] {
    if (!this.report) return [];
    if (!this.searchText.trim()) return this.report.products;

    const query = this.searchText.trim().toLowerCase();
    return this.report.products.filter(row =>
      row.productName.toLowerCase().includes(query) ||
      (row.packing ?? '').toLowerCase().includes(query)
    );
  }

  get selectedProductRow(): ProductStockRow | null {
    if (!this.report || !this.selectedRowProductId) return null;
    return this.report.products.find(p => p.productId === this.selectedRowProductId) ?? null;
  }

  get selectedMovements(): ProductStockMovement[] {
    if (!this.report || !this.selectedRowProductId) return [];
    return this.report.movements.filter(m => m.productId === this.selectedRowProductId);
  }

  onCategoryChange(): void {
    if (this.selectedProductId && !this.filteredProducts.some(p => p.id === this.selectedProductId)) {
      this.selectedProductId = null;
    }
    this.productOptions = this.buildProductOptions();
  }

  loadReport(): void {
    this.loading = true;
    this.selectedRowProductId = null;
    this.reportService.getReport({
      from: toDateInputValue(this.fromDate) || undefined,
      to: toDateInputValue(this.toDate) || undefined,
      categoryId: this.selectedCategoryId ?? undefined,
      productId: this.selectedProductId ?? undefined,
      includeDetails: true
    }).subscribe({
      next: (data) => {
        this.report = data;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load product stock report.');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  clearFilters(): void {
    this.fromDate = null;
    this.toDate = null;
    this.searchText = '';
    this.selectedCategoryId = null;
    this.selectedProductId = null;
    this.selectedRowProductId = null;
    this.loadReport();
  }

  selectProductRow(productId: number): void {
    this.selectedRowProductId = productId;
  }

  isSelectedRow(productId: number): boolean {
    return this.selectedRowProductId === productId;
  }

  getStockHealthClass(row: ProductStockRow): string {
    if (row.closing.kg <= 0) return 'health-empty';
    return 'health-ok';
  }

  closeDrilldown(): void {
    this.selectedRowProductId = null;
  }

  openPrint(): void {
    const queryParams: Record<string, string> = {};
    const fromDateStr = toDateInputValue(this.fromDate);
    const toDateStr = toDateInputValue(this.toDate);
    if (fromDateStr) queryParams['from'] = fromDateStr;
    if (toDateStr) queryParams['to'] = toDateStr;
    if (this.selectedCategoryId !== null) queryParams['categoryId'] = this.selectedCategoryId.toString();
    if (this.selectedProductId !== null) queryParams['productId'] = this.selectedProductId.toString();
    if (this.selectedRowProductId !== null) queryParams['selectedMovementProductId'] = this.selectedRowProductId.toString();

    const url = this.router.serializeUrl(this.router.createUrlTree(['/print-product-stock-report'], { queryParams }));
    window.open(url, '_blank');
  }

  formatDate(value: string): string {
    return new Date(value).toLocaleDateString('en-GB', {
      day: '2-digit',
      month: 'short',
      year: 'numeric'
    });
  }

  private buildProductOptions(): SelectOption[] {
    const filtered = this.selectedCategoryId
      ? this.products.filter(p => p.categoryId === this.selectedCategoryId)
      : this.products;
    return [
      { value: null as any, label: 'All Products' },
      ...filtered.map(p => ({
        value: p.id,
        label: p.name,
        sublabel: p.packing || ''
      }))
    ];
  }
}
