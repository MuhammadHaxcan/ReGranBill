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
    const qty = this.toNumber(line.qty);
    if (this.isPackedLine(line.rbp)) {
      return this.toNumber(line.packingWeightKg) * qty;
    }
    return qty;
  }

  getLineAmount(line: PurchaseVoucherProductLine): number {
    const qty = this.toNumber(line.qty);
    const rate = this.toNumber(line.rate);
    if (this.isPackedLine(line.rbp)) {
      return this.getLineTotalWeight(line) * rate;
    }
    return qty * rate;
  }

  get totalBags(): number {
    return this.lines
      .filter(line => this.isPackedLine(line.rbp))
      .reduce((sum: number, line) => sum + this.toNumber(line.qty), 0);
  }

  get totalWeight(): number {
    return this.lines.reduce((sum: number, line) => sum + this.getLineTotalWeight(line), 0);
  }

  get totalAmount(): number {
    return this.lines.reduce((sum: number, line) => sum + this.getLineAmount(line), 0);
  }

  saveRates(): void {
    if (this.challanId) {
      const rateUpdates: PurchaseVoucherRateUpdateRequest = {
        lines: this.lines.map(line => ({ entryId: line.id, rate: this.toNumber(line.rate) }))
      };

      this.purchaseService.updateRates(this.challanId, rateUpdates).subscribe({
        next: updatedDc => {
          this.journalVouchers = updatedDc.journalVouchers || [];
          this.ratesAdded = updatedDc.ratesAdded;
          this.toast.success(`${this.voucherNumber} rates saved successfully.`);
          this.router.navigate(['/pending-purchases']);
        },
        error: err => {
          this.toast.error(err?.error?.message || 'Unable to save rates.');
        }
      });
    }
  }

  cancel(): void {
    this.router.navigate(['/pending-purchases']);
  }

  private isPackedLine(rbp: string | undefined | null): boolean {
    return String(rbp ?? 'Yes').trim().toLowerCase() === 'yes';
  }

  private toNumber(value: unknown): number {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
  }
}
