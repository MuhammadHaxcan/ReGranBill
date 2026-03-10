import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { Router } from '@angular/router';
import { PurchaseVoucherService } from '../../services/purchase-voucher.service';
import { ToastService } from '../../services/toast.service';

@Component({
  selector: 'app-pending-purchases',
  templateUrl: './pending-purchases.component.html',
  styleUrl: './pending-purchases.component.css',
  standalone: false
})
export class PendingPurchasesComponent implements OnInit {
  challans: any[] = [];
  loading = true;

  constructor(
    private purchaseService: PurchaseVoucherService,
    private router: Router,
    private cdr: ChangeDetectorRef,
    private toast: ToastService
  ) {}

  ngOnInit(): void {
    this.loadChallans();
  }

  loadChallans(): void {
    this.loading = true;
    this.purchaseService.getAll().subscribe({
      next: data => {
        this.challans = data;
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

  getTotalBags(dc: any): number {
    return dc.lines.reduce((sum: number, line: any) => sum + (line.qty || 0), 0);
  }

  getTotalWeight(dc: any): number {
    return dc.lines.reduce((sum: number, line: any) => {
      return sum + (line.packingWeightKg * line.qty);
    }, 0);
  }

  getTotalAmount(dc: any): number {
    return dc.lines.reduce((sum: number, line: any) => {
      return sum + (line.packingWeightKg * line.qty * line.rate);
    }, 0);
  }

  hasRates(dc: any): boolean {
    return dc.ratesAdded || dc.lines.some((line: any) => line.rate > 0);
  }

  getFormattedDate(date: string): string {
    return new Date(date).toLocaleDateString('en-GB', {
      day: '2-digit',
      month: 'short',
      year: 'numeric'
    });
  }

  getProductCount(dc: any): number {
    return dc.lines.length;
  }

  editChallan(dc: any): void {
    this.router.navigate(['/purchase-voucher', dc.id]);
  }

  addRate(dc: any): void {
    this.router.navigate(['/add-purchase-rate', dc.id]);
  }

  printChallan(dc: any): void {
    this.purchaseService.openPdfInNewTab(dc.id);
  }
}
