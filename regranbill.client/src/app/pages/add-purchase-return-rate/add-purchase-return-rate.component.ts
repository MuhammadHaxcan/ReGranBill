import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { PurchaseReturnService, PurchaseReturnViewModel } from '../../services/purchase-return.service';
import { ToastService } from '../../services/toast.service';
import { DeliveryChallanLineViewModel } from '../../services/purchase-return.service';
import {
  getPurchaseLineWeight,
  getPurchaseLineAmount,
  getPurchaseTotalBags,
  getPurchaseTotalWeight,
  getPurchaseTotalAmount
} from '../../utils/delivery-calculations';
import { formatDateDdMmYyyy, parseLocalDate } from '../../utils/date-utils';

@Component({
  selector: 'app-add-purchase-return-rate',
  templateUrl: './add-purchase-return-rate.component.html',
  styleUrl: './add-purchase-return-rate.component.css',
  standalone: false
})
export class AddPurchaseReturnRateComponent implements OnInit {
  purchaseReturnId: number | null = null;
  prNumber = '';
  purchaseReturnDate = new Date();
  vendorName = '';
  description = '';
  ratesAdded = false;
  loading = true;

  lines: DeliveryChallanLineViewModel[] = [];
  journalVouchers: any[] = [];

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private prService: PurchaseReturnService,
    private cdr: ChangeDetectorRef,
    private toast: ToastService
  ) {}

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.purchaseReturnId = +idParam;
      this.loadData();
    }
  }

  loadData(): void {
    this.prService.getById(this.purchaseReturnId!).subscribe({
      next: (pr: PurchaseReturnViewModel) => {
        if (pr) {
          this.prNumber = pr.prNumber;
          this.purchaseReturnDate = parseLocalDate(pr.date);
          this.vendorName = pr.vendorName || '—';
          this.description = pr.description || '';
          this.ratesAdded = pr.ratesAdded;
          this.lines = pr.lines;
          this.journalVouchers = pr.journalVouchers || [];
        }
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load purchase return details.');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  get formattedDate(): string {
    return formatDateDdMmYyyy(this.purchaseReturnDate);
  }

  getLineTotalWeight(line: DeliveryChallanLineViewModel): number {
    return getPurchaseLineWeight(line);
  }

  getLineAmount(line: DeliveryChallanLineViewModel): number {
    return getPurchaseLineAmount(line);
  }

  get totalBags(): number {
    return getPurchaseTotalBags(this.lines as any);
  }

  get totalWeight(): number {
    return getPurchaseTotalWeight(this.lines as any);
  }

  get totalAmount(): number {
    return getPurchaseTotalAmount(this.lines as any);
  }

  saveRates(): void {
    if (this.purchaseReturnId) {
      const rateUpdates = this.lines.map(line => ({ entryId: line.id, rate: line.rate }));
      this.prService.updateRates(this.purchaseReturnId, { lines: rateUpdates }).subscribe({
        next: updatedPr => {
          this.journalVouchers = updatedPr.journalVouchers || [];
          this.ratesAdded = updatedPr.ratesAdded;
          this.toast.success(`${this.prNumber} rates saved successfully.`);
      this.router.navigate(['/pending']);
        },
        error: err => {
          this.toast.error(err?.error?.message || 'Unable to save rates.');
        }
      });
    }
  }

  cancel(): void {
      this.router.navigate(['/pending']);
  }
}
