import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { ProductionVoucherService } from '../../services/production-voucher.service';
import { ToastService } from '../../services/toast.service';
import { ConfirmModalService } from '../../services/confirm-modal.service';
import { ProductionVoucherListDto } from '../../models/production-voucher.model';
import { formatDateDdMmYyyy } from '../../utils/date-utils';
import { getApiErrorMessage } from '../../utils/api-error';

@Component({
  selector: 'app-pending-productions',
  templateUrl: './pending-productions.component.html',
  styleUrl: './pending-productions.component.css',
  standalone: false
})
export class PendingProductionsComponent implements OnInit {
  vouchers: ProductionVoucherListDto[] = [];
  loading = true;

  constructor(
    private productionService: ProductionVoucherService,
    private router: Router,
    private toast: ToastService,
    private confirmModal: ConfirmModalService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.productionService.getAll().subscribe({
      next: data => {
        this.vouchers = data;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load production vouchers.');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  formatDate(value: string): string {
    return formatDateDdMmYyyy(value);
  }

  open(voucher: ProductionVoucherListDto): void {
    this.router.navigate(['/production-voucher', voucher.id]);
  }

  print(voucher: ProductionVoucherListDto, event: MouseEvent): void {
    event.stopPropagation();
    window.open(`/print-prod/${voucher.id}`, '_blank');
  }

  async delete(voucher: ProductionVoucherListDto, event: MouseEvent): Promise<void> {
    event.stopPropagation();
    const confirmed = await this.confirmModal.confirm({
      title: 'Delete Production Voucher',
      message: `Delete ${voucher.voucherNumber}? This cannot be undone.`,
      confirmText: 'Delete',
      cancelText: 'Cancel'
    });
    if (!confirmed) return;

    this.productionService.delete(voucher.id).subscribe({
      next: () => {
        this.toast.success(`${voucher.voucherNumber} deleted.`);
        this.load();
      },
      error: err => this.toast.error(getApiErrorMessage(err, 'Unable to delete voucher.'))
    });
  }
}
