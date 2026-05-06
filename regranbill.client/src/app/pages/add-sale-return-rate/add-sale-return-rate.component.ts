import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { SaleReturnService } from '../../services/sale-return.service';
import { ToastService } from '../../services/toast.service';
import { SaleReturnViewModel, JournalVoucherSummary } from '../../models/sale-return.model';
import { DeliveryChallanLineViewModel } from '../../models/delivery-challan.model';
import {
  getDeliveryLineAmount,
  getDeliveryLineWeight,
  getDeliveryTotalAmount,
  getDeliveryTotalBags,
  getDeliveryTotalWeight
} from '../../utils/delivery-calculations';
import { formatDateDdMmYyyy, parseLocalDate } from '../../utils/date-utils';
import { getApiErrorMessage } from '../../utils/api-error';

@Component({
  selector: 'app-add-sale-return-rate',
  templateUrl: './add-sale-return-rate.component.html',
  styleUrl: './add-sale-return-rate.component.css',
  standalone: false
})
export class AddSaleReturnRateComponent implements OnInit {
  saleReturnId: number | null = null;
  srNumber = '';
  saleReturnDate = new Date();
  customerName = '';
  description = '';
  ratesAdded = false;
  loading = true;

  lines: DeliveryChallanLineViewModel[] = [];
  journalVouchers: JournalVoucherSummary[] = [];

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private srService: SaleReturnService,
    private cdr: ChangeDetectorRef,
    private toast: ToastService
  ) {}

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.saleReturnId = +idParam;
      this.loadData();
    }
  }

  loadData(): void {
    this.srService.getById(this.saleReturnId!).subscribe({
      next: (sr: SaleReturnViewModel) => {
        if (sr) {
          this.srNumber = sr.srNumber;
          this.saleReturnDate = parseLocalDate(sr.date);
          this.customerName = sr.customerName || '\u2014';
          this.description = sr.description || '';
          this.ratesAdded = sr.ratesAdded;
          this.lines = sr.lines;
          this.journalVouchers = sr.journalVouchers || [];
        }
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load sale return details.');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  get formattedDate(): string {
    return formatDateDdMmYyyy(this.saleReturnDate);
  }

  getLineTotalWeight(line: DeliveryChallanLineViewModel): number {
    return getDeliveryLineWeight(line);
  }

  getLineAmount(line: DeliveryChallanLineViewModel): number {
    return getDeliveryLineAmount(line);
  }

  get totalBags(): number {
    return getDeliveryTotalBags(this.lines);
  }

  get totalWeight(): number {
    return getDeliveryTotalWeight(this.lines);
  }

  get totalAmount(): number {
    return getDeliveryTotalAmount(this.lines);
  }

  saveRates(): void {
    if (this.saleReturnId) {
      const rateUpdates = this.lines.map(line => ({ entryId: line.id, rate: line.rate }));
      this.srService.updateRates(this.saleReturnId, { lines: rateUpdates }).subscribe({
        next: updatedSr => {
          this.journalVouchers = updatedSr.journalVouchers || [];
          this.ratesAdded = updatedSr.ratesAdded;
          this.toast.success(`${this.srNumber} rates saved successfully.`);
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
