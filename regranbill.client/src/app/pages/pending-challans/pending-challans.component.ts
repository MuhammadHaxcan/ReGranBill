import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { DeliveryChallanService } from '../../services/delivery-challan.service';
import { ToastService } from '../../services/toast.service';
import { ConfirmModalService } from '../../services/confirm-modal.service';
import { DeliveryChallanViewModel } from '../../models/delivery-challan.model';
import {
  getDeliveryTotalAmount,
  getDeliveryTotalBags,
  getDeliveryTotalWeight
} from '../../utils/delivery-calculations';
import { parseLocalDate } from '../../utils/date-utils';
import { getApiErrorMessage } from '../../utils/api-error';

@Component({
  selector: 'app-pending-challans',
  templateUrl: './pending-challans.component.html',
  styleUrl: './pending-challans.component.css',
  standalone: false
})
export class PendingChallansComponent implements OnInit {
  challans: DeliveryChallanViewModel[] = [];
  loading = true;

  constructor(
    private dcService: DeliveryChallanService,
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

  getTotalBags(dc: DeliveryChallanViewModel): number {
    return getDeliveryTotalBags(dc.lines);
  }

  getTotalWeight(dc: DeliveryChallanViewModel): number {
    return getDeliveryTotalWeight(dc.lines);
  }

  getTotalAmount(dc: DeliveryChallanViewModel): number {
    return getDeliveryTotalAmount(dc.lines);
  }

  hasRates(dc: DeliveryChallanViewModel): boolean {
    return dc.ratesAdded || dc.lines.some(line => line.rate > 0);
  }

  getFormattedDate(date: string): string {
    return new Intl.DateTimeFormat('en-GB', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric'
    }).format(parseLocalDate(date));
  }

  getProductCount(dc: DeliveryChallanViewModel): number {
    return dc.lines.length;
  }

  editChallan(dc: DeliveryChallanViewModel): void {
    this.router.navigate(['/delivery-challan', dc.id]);
  }

  addRate(dc: DeliveryChallanViewModel): void {
    this.router.navigate(['/add-rate', dc.id]);
  }

  printChallan(dc: DeliveryChallanViewModel): void {
    this.dcService.openPdfInNewTab(dc.id, dc.dcNumber);
  }

  async deleteChallan(dc: DeliveryChallanViewModel): Promise<void> {
    const confirmed = await this.confirmModal.confirm({
      title: 'Delete Challan',
      message: `Are you sure you want to delete "${dc.dcNumber}"? This action cannot be undone.`,
      confirmText: 'Delete',
      cancelText: 'Cancel'
    });
    if (!confirmed) return;

    this.dcService.delete(dc.id).subscribe({
      next: () => {
        this.toast.success(`${dc.dcNumber} deleted successfully.`);
        this.loadChallans();
      },
      error: err => {
        const msg = getApiErrorMessage(err, 'Unable to delete challan.');
        this.confirmModal.info({ title: 'Cannot Delete', message: msg });
      }
    });
  }

}
