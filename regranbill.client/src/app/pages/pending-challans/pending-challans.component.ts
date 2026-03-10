import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { Router } from '@angular/router';
import { DeliveryChallanService } from '../../services/delivery-challan.service';
import { ToastService } from '../../services/toast.service';

@Component({
  selector: 'app-pending-challans',
  templateUrl: './pending-challans.component.html',
  styleUrl: './pending-challans.component.css',
  standalone: false
})
export class PendingChallansComponent implements OnInit {
  challans: any[] = [];
  loading = true;

  constructor(
    private dcService: DeliveryChallanService,
    private router: Router,
    private cdr: ChangeDetectorRef,
    private toast: ToastService
  ) {}

  ngOnInit(): void {
    this.loadChallans();
  }

  loadChallans(): void {
    this.loading = true;
    this.dcService.getAll().subscribe({
      next: data => {
        this.challans = data;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load pending challans.');
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
    this.router.navigate(['/delivery-challan', dc.id]);
  }

  addRate(dc: any): void {
    this.router.navigate(['/add-rate', dc.id]);
  }

  printChallan(dc: any): void {
    this.dcService.openPdfInNewTab(dc.id);
  }
}
