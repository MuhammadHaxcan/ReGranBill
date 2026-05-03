import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { SaleReturnService } from '../../services/sale-return.service';
import { ToastService } from '../../services/toast.service';
import { ConfirmModalService } from '../../services/confirm-modal.service';
import { SaleReturnViewModel } from '../../models/sale-return.model';
import {
  getDeliveryTotalAmount,
  getDeliveryTotalBags,
  getDeliveryTotalWeight
} from '../../utils/delivery-calculations';
import { formatDateDdMmYyyy, parseLocalDate } from '../../utils/date-utils';
import { getApiErrorMessage } from '../../utils/api-error';

@Component({
  selector: 'app-pending-sale-returns',
  templateUrl: './pending-sale-returns.component.html',
  styleUrl: './pending-sale-returns.component.css',
  standalone: false
})
export class PendingSaleReturnsComponent implements OnInit {
  saleReturns: SaleReturnViewModel[] = [];
  loading = true;

  constructor(
    private saleReturnService: SaleReturnService,
    private router: Router,
    private cdr: ChangeDetectorRef,
    private toast: ToastService,
    private confirmModal: ConfirmModalService,
    public authService: AuthService
  ) {}

  get canSeeRates(): boolean {
    return this.authService.hasPage('voucher-rates');
  }

  ngOnInit(): void {
    this.loadSaleReturns();
  }

  loadSaleReturns(): void {
    this.loading = true;
    this.saleReturnService.getAll().subscribe({
      next: data => {
        this.saleReturns = data;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load pending sale returns.');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  getTotalBags(sr: SaleReturnViewModel): number {
    return getDeliveryTotalBags(sr.lines);
  }

  getTotalWeight(sr: SaleReturnViewModel): number {
    return getDeliveryTotalWeight(sr.lines);
  }

  getTotalAmount(sr: SaleReturnViewModel): number {
    return getDeliveryTotalAmount(sr.lines);
  }

  hasRates(sr: SaleReturnViewModel): boolean {
    return sr.ratesAdded || sr.lines.some(line => line.rate > 0);
  }

  getFormattedDate(date: string): string {
    return formatDateDdMmYyyy(date);
  }

  getProductCount(sr: SaleReturnViewModel): number {
    return sr.lines.length;
  }

  editSaleReturn(sr: SaleReturnViewModel): void {
    this.router.navigate(['/sale-return', sr.id]);
  }

  addRate(sr: SaleReturnViewModel): void {
    this.router.navigate(['/add-sale-return-rate', sr.id]);
  }

  printSaleReturn(sr: SaleReturnViewModel): void {
    this.saleReturnService.openPdfInNewTab(sr.id);
  }

  async deleteSaleReturn(sr: SaleReturnViewModel): Promise<void> {
    const confirmed = await this.confirmModal.confirm({
      title: 'Delete Sale Return',
      message: `Are you sure you want to delete "${sr.srNumber}"? This action cannot be undone.`,
      confirmText: 'Delete',
      cancelText: 'Cancel'
    });
    if (!confirmed) return;

    this.saleReturnService.delete(sr.id).subscribe({
      next: () => {
        this.toast.success(`${sr.srNumber} deleted successfully.`);
        this.loadSaleReturns();
      },
      error: err => {
        const msg = getApiErrorMessage(err, 'Unable to delete sale return.');
        this.confirmModal.info({ title: 'Cannot Delete', message: msg });
      }
    });
  }
}
