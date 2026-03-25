import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { Router } from '@angular/router';
import { PurchaseVoucherService } from '../../services/purchase-voucher.service';
import { ToastService } from '../../services/toast.service';
import { ConfirmModalService } from '../../services/confirm-modal.service';
import { PurchaseVoucherViewModel } from '../../models/purchase-voucher.model';
import { formatDateDdMmYyyy } from '../../utils/date-utils';

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
    private confirmModal: ConfirmModalService
  ) {}

  ngOnInit(): void {
    this.loadChallans();
  }

  loadChallans(): void {
    this.loading = true;
    this.purchaseService.getAll().subscribe({
      next: data => {
        this.vouchers = data;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load pending purchases.');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  getTotalBags(voucher: PurchaseVoucherViewModel): number {
    return voucher.lines.reduce((sum: number, line) => sum + (this.isPackedLine(line.rbp) ? this.toNumber(line.qty) : 0), 0);
  }

  getTotalWeight(voucher: PurchaseVoucherViewModel): number {
    return voucher.lines.reduce((sum: number, line) => {
      const qty = this.toNumber(line.qty);
      if (this.isPackedLine(line.rbp)) {
        return sum + (this.toNumber(line.packingWeightKg) * qty);
      }
      return sum + qty;
    }, 0);
  }

  getTotalAmount(voucher: PurchaseVoucherViewModel): number {
    return voucher.lines.reduce((sum: number, line) => {
      const qty = this.toNumber(line.qty);
      const rate = this.toNumber(line.rate);
      if (this.isPackedLine(line.rbp)) {
        return sum + (this.toNumber(line.packingWeightKg) * qty * rate);
      }
      return sum + (qty * rate);
    }, 0);
  }

  hasRates(voucher: PurchaseVoucherViewModel): boolean {
    return voucher.ratesAdded || voucher.lines.some(line => this.toNumber(line.rate) > 0);
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
        const msg = err?.error?.message || 'Unable to delete purchase voucher.';
        this.confirmModal.info({ title: 'Cannot Delete', message: msg });
      }
    });
  }

  private isPackedLine(rbp: string | undefined | null): boolean {
    return String(rbp ?? 'Yes').trim().toLowerCase() === 'yes';
  }

  private toNumber(value: unknown): number {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
  }
}
