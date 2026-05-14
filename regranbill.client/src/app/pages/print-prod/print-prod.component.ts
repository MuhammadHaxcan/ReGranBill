import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ProductionVoucherService } from '../../services/production-voucher.service';
import { CompanySettingsService } from '../../services/company-settings.service';
import { ProductionVoucherApiDto } from '../../models/production-voucher.model';
import { CompanySettings } from '../../models/company-settings.model';
import { formatDateDdMmYyyy } from '../../utils/date-utils';
import { ToastService } from '../../services/toast.service';
import { catchError, of } from 'rxjs';

@Component({
  selector: 'app-print-prod',
  templateUrl: './print-prod.component.html',
  styleUrl: './print-prod.component.css',
  standalone: false
})
export class PrintProdComponent implements OnInit {
  voucher: ProductionVoucherApiDto | null = null;
  company: CompanySettings | null = null;
  loading = true;

  constructor(
    private route: ActivatedRoute,
    private service: ProductionVoucherService,
    private companySettings: CompanySettingsService,
    private toast: ToastService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;

    this.companySettings.getSettings().pipe(catchError(() => of(null))).subscribe(c => this.company = c);

    this.service.getById(+id).subscribe({
      next: voucher => {
        this.voucher = voucher;
        this.loading = false;
        this.cdr.detectChanges();
        setTimeout(() => window.print(), 250);
      },
      error: () => {
        this.toast.error('Unable to load production voucher.');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  formatDate(value: string): string {
    return formatDateDdMmYyyy(value);
  }

  doPrint(): void {
    window.print();
  }
}
