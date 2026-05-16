import { Component, OnDestroy, OnInit } from '@angular/core';
import { Subscription } from 'rxjs';
import { BlockedDeleteData, BlockedDeleteModalService } from '../../services/blocked-delete-modal.service';

@Component({
  selector: 'app-blocked-delete-modal',
  templateUrl: './blocked-delete-modal.component.html',
  styleUrl: './blocked-delete-modal.component.css',
  standalone: false
})
export class BlockedDeleteModalComponent implements OnInit, OnDestroy {
  visible = false;
  data: BlockedDeleteData | null = null;
  private sub?: Subscription;

  constructor(private svc: BlockedDeleteModalService) {}

  ngOnInit(): void {
    this.sub = this.svc.modal$.subscribe(data => {
      if (data) {
        this.data = data;
        this.visible = true;
      } else {
        this.visible = false;
        this.data = null;
      }
    });
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  get remaining(): number {
    if (!this.data) return 0;
    return Math.max(0, this.data.totalCount - this.data.vouchers.length);
  }

  prettyType(voucherType: string): string {
    return voucherType.replace(/Voucher$/, '').replace(/([A-Z])/g, ' $1').trim();
  }

  close(): void {
    this.svc.close();
  }
}
