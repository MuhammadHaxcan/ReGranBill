import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { DeliveryChallanService } from '../../services/delivery-challan.service';

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
  status = 'Draft';
  ratesAdded = false;
  loading = true;

  lines: any[] = [];
  cartage: any = null;
  journalVouchers: any[] = [];

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private dcService: DeliveryChallanService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.challanId = +idParam;
      this.loadData();
    }
  }

  loadData(): void {
    this.dcService.getById(this.challanId!).subscribe(dc => {
      if (dc) {
        this.dcNumber = dc.dcNumber;
        this.dcDate = new Date(dc.date);
        this.customerName = dc.customerName || '—';
        this.description = dc.description || '';
        this.status = dc.status;
        this.ratesAdded = dc.ratesAdded;
        this.lines = dc.lines;
        this.cartage = dc.cartage;
        this.journalVouchers = dc.journalVouchers || [];
      }
      this.loading = false;
      this.cdr.detectChanges();
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
    return this.lines.reduce((sum: number, line: any) => sum + (line.qty || 0), 0);
  }

  get totalWeight(): number {
    return this.lines.reduce((sum: number, line: any) => sum + this.getLineTotalWeight(line), 0);
  }

  get totalAmount(): number {
    return this.lines.reduce((sum: number, line: any) => sum + this.getLineAmount(line), 0);
  }

  saveRates(): void {
    if (this.challanId) {
      const rateUpdates = this.lines.map((l: any) => ({ lineId: l.id, rate: l.rate }));
      this.dcService.updateRates(this.challanId, { lines: rateUpdates }).subscribe(updatedDc => {
        this.journalVouchers = updatedDc.journalVouchers || [];
        this.ratesAdded = updatedDc.ratesAdded;
        alert('Rates saved successfully. Journal entries updated.');
        this.router.navigate(['/pending-challans']);
      });
    }
  }

  cancel(): void {
    this.router.navigate(['/pending-challans']);
  }
}
