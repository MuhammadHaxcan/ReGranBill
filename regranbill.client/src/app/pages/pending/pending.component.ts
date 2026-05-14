import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { DeliveryChallanService } from '../../services/delivery-challan.service';
import { DeliveryChallanViewModel } from '../../models/delivery-challan.model';
import { SaleReturnService } from '../../services/sale-return.service';
import { SaleReturnViewModel } from '../../models/sale-return.model';
import { PurchaseVoucherService } from '../../services/purchase-voucher.service';
import { PurchaseVoucherViewModel } from '../../models/purchase-voucher.model';
import { PurchaseReturnService, PurchaseReturnViewModel } from '../../services/purchase-return.service';
import { ToastService } from '../../services/toast.service';
import { ConfirmModalService } from '../../services/confirm-modal.service';
import { formatDateDdMmYyyy } from '../../utils/date-utils';
import {
  getDeliveryTotalBags,
  getDeliveryTotalWeight,
  getPurchaseTotalBags,
  getPurchaseTotalWeight
} from '../../utils/delivery-calculations';
import { getApiErrorMessage } from '../../utils/api-error';

interface PendingRow {
  type: 'dc' | 'sr' | 'pv' | 'pr';
  id: number;
  number: string;
  date: string;
  partyName: string;
  productCount: number;
  bags: number;
  weight: number;
}

@Component({
  selector: 'app-pending',
  templateUrl: './pending.component.html',
  styleUrl: './pending.component.css',
  standalone: false
})
export class PendingComponent implements OnInit {
  rows: PendingRow[] = [];
  loading = true;

  constructor(
    private dcService: DeliveryChallanService,
    private srService: SaleReturnService,
    private pvService: PurchaseVoucherService,
    private prService: PurchaseReturnService,
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
    this.loadAll();
  }

  loadAll(): void {
    this.loading = true;
    this.rows = [];

    this.dcService.getAll().subscribe({
      next: data => {
        this.rows.push(...data.filter(dc => !this.hasRatesDc(dc)).map(dc => this.toRowDc(dc)));
        this.cdr.detectChanges();
      },
      error: () => { this.toast.error('Unable to load challans.'); this.cdr.detectChanges(); }
    });

    this.srService.getAll().subscribe({
      next: data => {
        this.rows.push(...data.filter(sr => !this.hasRatesSr(sr)).map(sr => this.toRowSr(sr)));
        this.cdr.detectChanges();
      },
      error: () => { this.toast.error('Unable to load sale returns.'); this.cdr.detectChanges(); }
    });

    this.pvService.getAll().subscribe({
      next: data => {
        this.rows.push(...data.filter(pv => !this.hasRatesPv(pv)).map(pv => this.toRowPv(pv)));
        this.cdr.detectChanges();
      },
      error: () => { this.toast.error('Unable to load purchases.'); this.cdr.detectChanges(); }
    });

    this.prService.getAll().subscribe({
      next: data => {
        this.rows.push(...data.filter(pr => !this.hasRatesPr(pr)).map(pr => this.toRowPr(pr)));
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load purchase returns.');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  private toRowDc(dc: DeliveryChallanViewModel): PendingRow {
    return {
      type: 'dc', id: dc.id, number: dc.dcNumber, date: dc.date,
      partyName: dc.customerName || '-', productCount: dc.lines.length,
      bags: getDeliveryTotalBags(dc.lines as any),
      weight: getDeliveryTotalWeight(dc.lines as any)
    };
  }

  private toRowSr(sr: SaleReturnViewModel): PendingRow {
    return {
      type: 'sr', id: sr.id, number: sr.srNumber, date: sr.date,
      partyName: sr.customerName || '-', productCount: sr.lines.length,
      bags: getDeliveryTotalBags(sr.lines as any),
      weight: getDeliveryTotalWeight(sr.lines as any)
    };
  }

  private toRowPv(pv: PurchaseVoucherViewModel): PendingRow {
    return {
      type: 'pv', id: pv.id, number: pv.voucherNumber, date: pv.date,
      partyName: pv.vendorName || '-', productCount: pv.lines.length,
      bags: getPurchaseTotalBags(pv.lines as any),
      weight: getPurchaseTotalWeight(pv.lines as any)
    };
  }

  private toRowPr(pr: PurchaseReturnViewModel): PendingRow {
    return {
      type: 'pr', id: pr.id, number: pr.prNumber, date: pr.date,
      partyName: pr.vendorName || '-', productCount: pr.lines.length,
      bags: getPurchaseTotalBags(pr.lines as any),
      weight: getPurchaseTotalWeight(pr.lines as any)
    };
  }

  // Status helpers
  private hasRatesDc(dc: DeliveryChallanViewModel): boolean {
    return dc.ratesAdded;
  }
  private hasRatesSr(sr: SaleReturnViewModel): boolean {
    return sr.ratesAdded;
  }
  private hasRatesPv(pv: PurchaseVoucherViewModel): boolean {
    return pv.ratesAdded;
  }
  private hasRatesPr(pr: PurchaseReturnViewModel): boolean {
    return pr.ratesAdded;
  }

  // Formatters
  fmt(date: string) { return formatDateDdMmYyyy(date); }

  getTypeLabel(type: string): string {
    switch (type) {
      case 'dc': return 'DC';
      case 'sr': return 'SR';
      case 'pv': return 'PV';
      case 'pr': return 'PR';
      default: return type.toUpperCase();
    }
  }

  edit(row: PendingRow): void {
    switch (row.type) {
      case 'dc': this.router.navigate(['/delivery-challan', row.id]); break;
      case 'sr': this.router.navigate(['/sale-return', row.id]); break;
      case 'pv': this.router.navigate(['/purchase-voucher', row.id]); break;
      case 'pr': this.router.navigate(['/purchase-return', row.id]); break;
    }
  }

  addRate(row: PendingRow): void {
    switch (row.type) {
      case 'dc': this.router.navigate(['/add-rate', row.id]); break;
      case 'sr': this.router.navigate(['/add-sale-return-rate', row.id]); break;
      case 'pv': this.router.navigate(['/add-purchase-rate', row.id]); break;
      case 'pr': this.router.navigate(['/add-purchase-return-rate', row.id]); break;
    }
  }

  print(row: PendingRow): void {
    switch (row.type) {
      case 'dc': this.dcService.openPdfInNewTab(row.id, row.number); break;
      case 'sr': this.srService.openPdfInNewTab(row.id, row.number); break;
      case 'pv': this.pvService.openPdfInNewTab(row.id, row.number); break;
      case 'pr': this.prService.openPdfInNewTab(row.id, row.number); break;
    }
  }

  async delete(row: PendingRow): Promise<void> {
    const labels: Record<string, string> = { dc: 'Challan', sr: 'Sale Return', pv: 'Purchase', pr: 'Purchase Return' };
    if (!await this.confirmModal.confirm({
      title: `Delete ${labels[row.type]}`,
      message: `Delete "${row.number}"? This cannot be undone.`,
      confirmText: 'Delete',
      cancelText: 'Cancel'
    })) return;

    const service = this.getService(row.type);
    service.delete(row.id).subscribe({
      next: () => { this.toast.success(`${row.number} deleted.`); this.loadAll(); },
      error: (err: any) => { this.confirmModal.info({ title: 'Cannot Delete', message: getApiErrorMessage(err, 'Unable to delete.') }); }
    });
  }

  private getService(type: string): any {
    switch (type) {
      case 'dc': return this.dcService;
      case 'sr': return this.srService;
      case 'pv': return this.pvService;
      case 'pr': return this.prService;
      default: return this.dcService;
    }
  }
}
