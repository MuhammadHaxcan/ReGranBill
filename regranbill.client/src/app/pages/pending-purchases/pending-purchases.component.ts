import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { PurchaseVoucherService } from '../../services/purchase-voucher.service';
import { ToastService } from '../../services/toast.service';
import { ConfirmModalService } from '../../services/confirm-modal.service';
import { PurchaseVoucherViewModel } from '../../models/purchase-voucher.model';
import { formatDateDdMmYyyy } from '../../utils/date-utils';
import { getPurchaseTotalAmount, getPurchaseTotalBags, getPurchaseTotalWeight, toNumber } from '../../utils/delivery-calculations';
import { getApiErrorMessage } from '../../utils/api-error';

@Component({
  selector: 'app-pending-purchases',
  templateUrl: './pending-purchases.component.html',
  styleUrl: './pending-purchases.component.css',
  standalone: false
})
export class PendingPurchasesComponent implements OnInit {
  vouchers: PurchaseVoucherViewModel[] = [];
  loading = true;

  constructor(
    private purchaseService: PurchaseVoucherService,
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
    this.loadChallans();
  }

  loadChallans(): void {
    this.loading = true;
    this.purchaseService.getAll().subscribe({
      next: data => {
        this.vouchers = data;
        this.loading = false;
        
      },
      error: () => {
        this.toast.error('Unable to load pending purchases.');
        this.loading = false;
        
      }
    });
  }

  getTotalBags(voucher: PurchaseVoucherViewModel): number {
    return getPurchaseTotalBags(voucher.lines);
  }

  getTotalWeight(voucher: PurchaseVoucherViewModel): number {
    return getPurchaseTotalWeight(voucher.lines);
  }

  getTotalAmount(voucher: PurchaseVoucherViewModel): number {
    return getPurchaseTotalAmount(voucher.lines);
  }

  hasRates(voucher: PurchaseVoucherViewModel): boolean {
    return voucher.ratesAdded || voucher.lines.some(line => toNumber(line.rate) > 0);
  }

  getFormattedDate(date: string): string {
    return formatDateDdMmYyyy(date);
  }

  getProductCount(voucher: PurchaseVoucherViewModel): number {
    return voucher.lines.length;
  }

  editChallan(voucher: PurchaseVoucherViewModel): void {
    this.router.navigate(['/purchase-voucher', voucher.id]);
  }

  addRate(voucher: PurchaseVoucherViewModel): void {
    this.router.navigate(['/add-purchase-rate', voucher.id]);
  }

  printChallan(voucher: PurchaseVoucherViewModel): void {
    this.purchaseService.openPdfInNewTab(voucher.id);
  }

  async deleteVoucher(voucher: PurchaseVoucherViewModel): Promise<void> {
    const confirmed = await this.confirmModal.confirm({
      title: 'Delete Purchase Voucher',
      message: `Are you sure you want to delete "${voucher.voucherNumber}"? This action cannot be undone.`,
      confirmText: 'Delete',
      cancelText: 'Cancel'
    });
    if (!confirmed) return;

    this.purchaseService.delete(voucher.id).subscribe({
      next: () => {
        this.toast.success(`${voucher.voucherNumber} deleted successfully.`);
        this.loadChallans();
      },
      error: err => {
        const msg = getApiErrorMessage(err, 'Unable to delete purchase voucher.');
        this.confirmModal.info({ title: 'Cannot Delete', message: msg });
      }
    });
  }

}
