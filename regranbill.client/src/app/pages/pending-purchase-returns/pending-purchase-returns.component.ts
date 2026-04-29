import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { Router } from '@angular/router';
import { PurchaseReturnService, PurchaseReturnViewModel } from '../../services/purchase-return.service';
import { ToastService } from '../../services/toast.service';
import { ConfirmModalService } from '../../services/confirm-modal.service';
import { formatDateDdMmYyyy } from '../../utils/date-utils';
import { getPurchaseTotalAmount, getPurchaseTotalBags, getPurchaseTotalWeight } from '../../utils/delivery-calculations';

@Component({
  selector: 'app-pending-purchase-returns',
  templateUrl: './pending-purchase-returns.component.html',
  styleUrl: './pending-purchase-returns.component.css',
  standalone: false
})
export class PendingPurchaseReturnsComponent implements OnInit {
  purchaseReturns: PurchaseReturnViewModel[] = [];
  loading = true;

  constructor(
    private purchaseReturnService: PurchaseReturnService,
    private router: Router,
    private cdr: ChangeDetectorRef,
    private toast: ToastService,
    private confirmModal: ConfirmModalService
  ) {}

  ngOnInit(): void {
    this.loadPurchaseReturns();
  }

  loadPurchaseReturns(): void {
    this.loading = true;
    this.purchaseReturnService.getAll().subscribe({
      next: data => {
        this.purchaseReturns = data;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load pending purchase returns.');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  getTotalBags(pr: PurchaseReturnViewModel): number {
    return getPurchaseTotalBags(pr.lines as any);
  }

  getTotalWeight(pr: PurchaseReturnViewModel): number {
    return getPurchaseTotalWeight(pr.lines as any);
  }

  getTotalAmount(pr: PurchaseReturnViewModel): number {
    return getPurchaseTotalAmount(pr.lines as any);
  }

  hasRates(pr: PurchaseReturnViewModel): boolean {
    return pr.ratesAdded || pr.lines.some((line: any) => line.rate > 0);
  }

  getFormattedDate(date: string): string {
    return formatDateDdMmYyyy(date);
  }

  getProductCount(pr: PurchaseReturnViewModel): number {
    return pr.lines.length;
  }

  editPurchaseReturn(pr: PurchaseReturnViewModel): void {
    this.router.navigate(['/purchase-return', pr.id]);
  }

  addRate(pr: PurchaseReturnViewModel): void {
    this.router.navigate(['/add-purchase-return-rate', pr.id]);
  }

  printPurchaseReturn(pr: PurchaseReturnViewModel): void {
    this.purchaseReturnService.openPdfInNewTab(pr.id);
  }

  async deletePurchaseReturn(pr: PurchaseReturnViewModel): Promise<void> {
    const confirmed = await this.confirmModal.confirm({
      title: 'Delete Purchase Return',
      message: `Are you sure you want to delete "${pr.prNumber}"? This action cannot be undone.`,
      confirmText: 'Delete',
      cancelText: 'Cancel'
    });
    if (!confirmed) return;

    this.purchaseReturnService.delete(pr.id).subscribe({
      next: () => {
        this.toast.success(`${pr.prNumber} deleted successfully.`);
        this.loadPurchaseReturns();
      },
      error: err => {
        const msg = err?.error?.message || 'Unable to delete purchase return.';
        this.confirmModal.info({ title: 'Cannot Delete', message: msg });
      }
    });
  }
}