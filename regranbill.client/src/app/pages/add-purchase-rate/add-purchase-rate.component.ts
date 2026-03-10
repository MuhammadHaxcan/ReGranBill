import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import { PurchaseVoucherService } from '../../services/purchase-voucher.service';
import { ToastService } from '../../services/toast.service';

@Component({
  selector: 'app-add-purchase-rate',
  templateUrl: './add-purchase-rate.component.html',
  styleUrl: './add-purchase-rate.component.css',
  standalone: false
})
export class AddPurchaseRateComponent implements OnInit {
  challanId: number | null = null;
  dcNumber = '';
  dcDate = new Date();
  vendorName = '';
  description = '';
  ratesAdded = false;
  loading = true;

  lines: any[] = [];
  cartage: any = null;
  journalVouchers: any[] = [];

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
      next: dc => {
        if (dc) {
          this.dcNumber = dc.dcNumber;
          this.dcDate = new Date(dc.date);
          this.vendorName = dc.customerName || '-';
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
        this.toast.error('Unable to load purchase voucher details.');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  get formattedDate(): string {
    return this.dcDate.toLocaleDateString('en-GB', {
      day: '2-digit',
      month: 'short',
      year: 'numeric'
    });
  }

  getLineTotalWeight(line: any): number {
    return (line.packingWeightKg || 0) * (line.qty || 0);
  }

  getLineAmount(line: any): number {
    if (line.rbp === 'Yes') {
      return this.getLineTotalWeight(line) * (line.rate || 0);
    }
    return (line.qty || 0) * (line.rate || 0);
  }

  get totalBags(): number {
    return this.lines
      .filter((line: any) => line.rbp === 'Yes')
      .reduce((sum: number, line: any) => sum + (line.qty || 0), 0);
  }

  get totalWeight(): number {
    return this.lines.reduce((sum: number, line: any) => sum + this.getLineTotalWeight(line), 0);
  }

  get totalAmount(): number {
    return this.lines.reduce((sum: number, line: any) => sum + this.getLineAmount(line), 0);
  }

  saveRates(): void {
    if (this.challanId) {
      const rateUpdates = this.lines.map((l: any) => ({ entryId: l.id, rate: l.rate }));
      this.purchaseService.updateRates(this.challanId, { lines: rateUpdates }).subscribe({
        next: updatedDc => {
          this.journalVouchers = updatedDc.journalVouchers || [];
          this.ratesAdded = updatedDc.ratesAdded;
          this.toast.success(`${this.dcNumber} rates saved successfully.`);
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
}
