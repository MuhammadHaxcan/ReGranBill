import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { PurchaseVoucherService } from '../../services/purchase-voucher.service';
import { ToastService } from '../../services/toast.service';
import {
  PurchaseVoucherCartage,
  PurchaseVoucherJournalSummary,
  PurchaseVoucherProductLine,
  PurchaseVoucherViewModel,
  PurchaseVoucherRateUpdateRequest
} from '../../models/purchase-voucher.model';
import { formatDateDdMmYyyy, parseLocalDate } from '../../utils/date-utils';
import {
  getPurchaseAverageWeightPerBag,
  getPurchaseLineAmount,
  getPurchaseLineAverageWeight,
  getPurchaseLineWeight,
  getPurchaseTotalAmount,
  getPurchaseTotalBags,
  getPurchaseTotalWeight,
  toNumber
} from '../../utils/delivery-calculations';
import { getApiErrorMessage } from '../../utils/api-error';

@Component({
  selector: 'app-add-purchase-rate',
  templateUrl: './add-purchase-rate.component.html',
  styleUrl: './add-purchase-rate.component.css',
  standalone: false
})
export class AddPurchaseRateComponent implements OnInit {
  challanId: number | null = null;
  voucherNumber = '';
  voucherDate = new Date();
  vendorName = '';
  description = '';
  ratesAdded = false;
  loading = true;

  lines: PurchaseVoucherProductLine[] = [];
  cartage: PurchaseVoucherCartage | null = null;
  journalVouchers: PurchaseVoucherJournalSummary[] = [];

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private purchaseService: PurchaseVoucherService,
    private cdr: ChangeDetectorRef,
    private toast: ToastService
  ) {}

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.challanId = +idParam;
      this.loadData();
    }
  }

  loadData(): void {
    this.purchaseService.getById(this.challanId!).subscribe({
      next: (voucher: PurchaseVoucherViewModel) => {
        if (voucher) {
          this.voucherNumber = voucher.voucherNumber;
          this.voucherDate = parseLocalDate(voucher.date);
          this.vendorName = voucher.vendorName || '-';
          this.description = voucher.description || '';
          this.ratesAdded = voucher.ratesAdded;
          this.lines = voucher.lines;
          this.cartage = voucher.cartage;
          this.journalVouchers = voucher.journalVouchers || [];
        }
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load purchase voucher details.');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  get formattedDate(): string {
    return formatDateDdMmYyyy(this.voucherDate);
  }

  getLineTotalWeight(line: PurchaseVoucherProductLine): number {
    return getPurchaseLineWeight(line);
  }

  getLineAverageWeight(line: PurchaseVoucherProductLine): number {
    return getPurchaseLineAverageWeight(line);
  }

  getLineAmount(line: PurchaseVoucherProductLine): number {
    return getPurchaseLineAmount(line);
  }

  get totalBags(): number {
    return getPurchaseTotalBags(this.lines);
  }

  get totalWeight(): number {
    return getPurchaseTotalWeight(this.lines);
  }

  get avgWeightPerBag(): number {
    return getPurchaseAverageWeightPerBag(this.lines);
  }

  get totalAmount(): number {
    return getPurchaseTotalAmount(this.lines);
  }

  saveRates(): void {
    if (this.challanId) {
      const rateUpdates: PurchaseVoucherRateUpdateRequest = {
        lines: this.lines.map(line => ({ entryId: line.id, rate: toNumber(line.rate) }))
      };

      this.purchaseService.updateRates(this.challanId, rateUpdates).subscribe({
        next: updatedDc => {
          this.journalVouchers = updatedDc.journalVouchers || [];
          this.ratesAdded = updatedDc.ratesAdded;
          this.toast.success(`${this.voucherNumber} rates saved successfully.`);
      this.router.navigate(['/pending']);
        },
        error: err => {
          this.toast.error(getApiErrorMessage(err, 'Unable to save rates.'));
        }
      });
    }
  }

  cancel(): void {
      this.router.navigate(['/pending']);
  }

}
