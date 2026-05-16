import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { AuthService } from '../../services/auth.service';
import { DeliveryChallanService } from '../../services/delivery-challan.service';
import { DeliveryChallanViewModel } from '../../models/delivery-challan.model';
import { SaleReturnService } from '../../services/sale-return.service';
import { SaleReturnViewModel } from '../../models/sale-return.model';
import { PurchaseVoucherService } from '../../services/purchase-voucher.service';
import { PurchaseVoucherViewModel } from '../../models/purchase-voucher.model';
import { PurchaseReturnService, PurchaseReturnViewModel } from '../../services/purchase-return.service';
import { WashingVoucherService } from '../../services/washing-voucher.service';
import { ProductionVoucherService } from '../../services/production-voucher.service';
import { ToastService } from '../../services/toast.service';
import { ConfirmModalService } from '../../services/confirm-modal.service';
import { WashingVoucherListDto } from '../../models/washing-voucher.model';
import { ProductionVoucherListDto } from '../../models/production-voucher.model';
import { formatDateDdMmYyyy } from '../../utils/date-utils';
import { getApiErrorMessage } from '../../utils/api-error';
import {
  getDeliveryTotalAmount,
  getDeliveryTotalBags,
  getDeliveryTotalWeight,
  getPurchaseTotalAmount,
  getPurchaseTotalBags,
  getPurchaseTotalWeight
} from '../../utils/delivery-calculations';

interface RatedRow {
  type: 'dc' | 'sr' | 'pv' | 'pr' | 'wsh' | 'prod';
  id: number;
  number: string;
  date: string;
  partyName: string;
  productCount: number;
  bags: number;
  weight: number;
  amount: number;
}

@Component({
  selector: 'app-rated-vouchers',
  templateUrl: './rated-vouchers.component.html',
  styleUrl: './rated-vouchers.component.css',
  standalone: false
})
export class RatedVouchersComponent implements OnInit {
  rows: RatedRow[] = [];
  loading = true;

  constructor(
    private dcService: DeliveryChallanService,
    private srService: SaleReturnService,
    private pvService: PurchaseVoucherService,
    private prService: PurchaseReturnService,
    private washingService: WashingVoucherService,
    private prodService: ProductionVoucherService,
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
    forkJoin({
      dcs: this.dcService.getAll(),
      srs: this.srService.getAll(),
      pvs: this.pvService.getAll(),
      prs: this.prService.getAll(),
      wshs: this.washingService.getAll(),
      prods: this.prodService.getAll()
    }).subscribe({
      next: ({ dcs, srs, pvs, prs, wshs, prods }) => {
        this.rows = [
          ...dcs.filter(dc => dc.ratesAdded).map(dc => this.toRowDc(dc)),
          ...srs.filter(sr => sr.ratesAdded).map(sr => this.toRowSr(sr)),
          ...pvs.filter(pv => pv.ratesAdded).map(pv => this.toRowPv(pv)),
          ...prs.filter(pr => pr.ratesAdded).map(pr => this.toRowPr(pr)),
          ...wshs.map(wsh => this.toRowWsh(wsh)),
          ...prods.map(p => this.toRowProd(p))
        ].sort((a, b) => {
          const dateDiff = new Date(b.date).getTime() - new Date(a.date).getTime();
          if (dateDiff !== 0) return dateDiff;
          return b.id - a.id;
        });
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load rated vouchers.');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  private toRowDc(dc: DeliveryChallanViewModel): RatedRow {
    return {
      type: 'dc', id: dc.id, number: dc.dcNumber, date: dc.date,
      partyName: dc.customerName || '-', productCount: dc.lines.length,
      bags: getDeliveryTotalBags(dc.lines as any),
      weight: getDeliveryTotalWeight(dc.lines as any),
      amount: getDeliveryTotalAmount(dc.lines as any)
    };
  }

  private toRowSr(sr: SaleReturnViewModel): RatedRow {
    return {
      type: 'sr', id: sr.id, number: sr.srNumber, date: sr.date,
      partyName: sr.customerName || '-', productCount: sr.lines.length,
      bags: getDeliveryTotalBags(sr.lines as any),
      weight: getDeliveryTotalWeight(sr.lines as any),
      amount: getDeliveryTotalAmount(sr.lines as any)
    };
  }

  private toRowPv(pv: PurchaseVoucherViewModel): RatedRow {
    return {
      type: 'pv', id: pv.id, number: pv.voucherNumber, date: pv.date,
      partyName: pv.vendorName || '-', productCount: pv.lines.length,
      bags: getPurchaseTotalBags(pv.lines as any),
      weight: getPurchaseTotalWeight(pv.lines as any),
      amount: getPurchaseTotalAmount(pv.lines as any)
    };
  }

  private toRowPr(pr: PurchaseReturnViewModel): RatedRow {
    return {
      type: 'pr', id: pr.id, number: pr.prNumber, date: pr.date,
      partyName: pr.vendorName || '-', productCount: pr.lines.length,
      bags: getPurchaseTotalBags(pr.lines as any),
      weight: getPurchaseTotalWeight(pr.lines as any),
      amount: getPurchaseTotalAmount(pr.lines as any)
    };
  }

  private toRowWsh(wsh: WashingVoucherListDto): RatedRow {
    return {
      type: 'wsh',
      id: wsh.id,
      number: wsh.voucherNumber,
      date: wsh.date,
      partyName: wsh.sourceVendorName || '-',
      productCount: wsh.outputLineCount || 1,
      bags: 0,
      weight: wsh.outputWeightKg,
      amount: wsh.washedDebit
    };
  }

  private toRowProd(p: ProductionVoucherListDto): RatedRow {
    return {
      type: 'prod',
      id: p.id,
      number: p.voucherNumber,
      date: p.date,
      partyName: p.description || (p.lotNumber ? `Lot ${p.lotNumber}` : '-'),
      productCount: 1,
      bags: 0,
      weight: p.totalOutputKg,
      amount: p.totalInputCost
    };
  }

  fmt(date: string): string {
    return formatDateDdMmYyyy(date);
  }

  getTypeLabel(type: string): string {
    switch (type) {
      case 'dc': return 'DC';
      case 'sr': return 'SR';
      case 'pv': return 'PV';
      case 'pr': return 'PR';
      case 'wsh': return 'WSH';
      case 'prod': return 'PROD';
      default: return type.toUpperCase();
    }
  }

  view(row: RatedRow): void {
    switch (row.type) {
      case 'dc': this.router.navigate(['/delivery-challan', row.id]); break;
      case 'sr': this.router.navigate(['/sale-return', row.id]); break;
      case 'pv': this.router.navigate(['/purchase-voucher', row.id]); break;
      case 'pr': this.router.navigate(['/purchase-return', row.id]); break;
      case 'wsh': this.router.navigate(['/washing-voucher', row.id]); break;
      case 'prod': this.router.navigate(['/production-voucher', row.id]); break;
    }
  }

  print(row: RatedRow): void {
    switch (row.type) {
      case 'dc': this.dcService.openPdfInNewTab(row.id, row.number); break;
      case 'sr': this.srService.openPdfInNewTab(row.id, row.number); break;
      case 'pv': this.pvService.openPdfInNewTab(row.id, row.number); break;
      case 'pr': this.prService.openPdfInNewTab(row.id, row.number); break;
      case 'wsh': this.washingService.openPrintInNewTab(row.id, row.number); break;
      case 'prod': this.prodService.openPdfInNewTab(row.id, row.number); break;
    }
  }

  async delete(row: RatedRow): Promise<void> {
    const labels: Record<RatedRow['type'], string> = {
      dc: 'Delivery Challan',
      sr: 'Sale Return',
      pv: 'Purchase Voucher',
      pr: 'Purchase Return',
      wsh: 'Washing Voucher',
      prod: 'Production Voucher'
    };

    const confirmed = await this.confirmModal.confirm({
      title: `Delete ${labels[row.type]}`,
      message: `Delete "${row.number}"? This cannot be undone.`,
      confirmText: 'Delete',
      cancelText: 'Cancel'
    });
    if (!confirmed) return;

    this.getDeleteService(row.type).delete(row.id).subscribe({
      next: () => {
        this.toast.success(`${row.number} deleted.`);
        this.loadAll();
      },
      error: (err: any) => {
        this.toast.error(getApiErrorMessage(err, 'Unable to delete voucher.'));
      }
    });
  }

  private getDeleteService(type: RatedRow['type']): { delete(id: number): any } {
    switch (type) {
      case 'dc': return this.dcService;
      case 'sr': return this.srService;
      case 'pv': return this.pvService;
      case 'pr': return this.prService;
      case 'wsh': return this.washingService;
      case 'prod': return this.prodService;
    }
  }
}
