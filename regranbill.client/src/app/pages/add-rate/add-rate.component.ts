import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { DeliveryChallanService } from '../../services/delivery-challan.service';
import { ToastService } from '../../services/toast.service';
import {
  DeliveryCartageViewModel,
  DeliveryChallanLineViewModel,
  DeliveryChallanViewModel,
  JournalVoucherSummary
} from '../../models/delivery-challan.model';
import {
  getDeliveryLineAmount,
  getDeliveryLineWeight,
  getDeliveryTotalAmount,
  getDeliveryTotalBags,
  getDeliveryTotalWeight
} from '../../utils/delivery-calculations';
import { parseLocalDate } from '../../utils/date-utils';
import { getApiErrorMessage } from '../../utils/api-error';

@Component({
  selector: 'app-add-rate',
  templateUrl: './add-rate.component.html',
  styleUrl: './add-rate.component.css',
  standalone: false
})
export class AddRateComponent implements OnInit {
  challanId: number | null = null;
  dcNumber = '';
  dcDate = new Date();
  customerName = '';
  description = '';
  ratesAdded = false;
  loading = true;

  lines: DeliveryChallanLineViewModel[] = [];
  cartage: DeliveryCartageViewModel | null = null;
  journalVouchers: JournalVoucherSummary[] = [];

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private dcService: DeliveryChallanService,
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
    this.dcService.getById(this.challanId!).subscribe({
      next: (dc: DeliveryChallanViewModel) => {
        if (dc) {
          this.dcNumber = dc.dcNumber;
          this.dcDate = parseLocalDate(dc.date);
          this.customerName = dc.customerName || '\u2014';
          this.description = dc.description || '';
          this.ratesAdded = dc.ratesAdded;
          this.lines = dc.lines;
          this.cartage = dc.cartage;
          this.journalVouchers = dc.journalVouchers || [];
        }
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load challan details.');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  get formattedDate(): string {
    return new Intl.DateTimeFormat('en-GB', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric'
    }).format(this.dcDate);
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
    if (this.challanId) {
      const rateUpdates = this.lines.map(line => ({ entryId: line.id, rate: line.rate }));
      this.dcService.updateRates(this.challanId, { lines: rateUpdates }).subscribe({
        next: updatedDc => {
          this.journalVouchers = updatedDc.journalVouchers || [];
          this.ratesAdded = updatedDc.ratesAdded;
          this.toast.success(`${this.dcNumber} rates saved successfully.`);
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
