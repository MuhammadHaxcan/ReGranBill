import { Component, Input } from '@angular/core';
import { DownstreamUsage } from '../../services/downstream-usage.service';

@Component({
  selector: 'app-downstream-usage-banner',
  templateUrl: './downstream-usage-banner.component.html',
  styleUrl: './downstream-usage-banner.component.css',
  standalone: false
})
export class DownstreamUsageBannerComponent {
  @Input() usages: DownstreamUsage[] = [];
  @Input() sourceLabel = 'voucher';

  get hasUsage(): boolean {
    return this.usages && this.usages.length > 0;
  }

  routeFor(usage: DownstreamUsage): string[] | null {
    const map: Record<string, string> = {
      PurchaseVoucher: '/purchase-voucher',
      PurchaseReturnVoucher: '/purchase-return',
      SaleVoucher: '/delivery-challan',
      SaleReturnVoucher: '/sale-return',
      WashingVoucher: '/washing-voucher',
      ProductionVoucher: '/production-voucher',
      JournalVoucher: '/journal-voucher',
      ReceiptVoucher: '/receipt-voucher',
      PaymentVoucher: '/payment-voucher'
    };
    const prefix = map[usage.voucherType];
    return prefix ? [prefix, String(usage.voucherId)] : null;
  }

  prettyType(voucherType: string): string {
    return voucherType.replace(/Voucher$/, '').replace(/([A-Z])/g, ' $1').trim();
  }

  prettyTxType(txType: string): string {
    return txType.replace(/([A-Z])/g, ' $1').trim();
  }
}
