import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';

export interface BlockedVoucherRef {
  id: number;
  voucherNumber: string;
  date: string;
  voucherType: string;
}

export interface BlockedDeleteData {
  title: string;
  message: string;
  vouchers: BlockedVoucherRef[];
  totalCount: number;
}

@Injectable({ providedIn: 'root' })
export class BlockedDeleteModalService {
  private subject = new Subject<BlockedDeleteData | null>();
  modal$ = this.subject.asObservable();

  show(data: BlockedDeleteData): void {
    this.subject.next(data);
  }

  close(): void {
    this.subject.next(null);
  }
}
