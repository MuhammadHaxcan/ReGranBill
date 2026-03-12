export type CashVoucherMode = 'receipt' | 'payment';

export interface CashVoucherLine {
  id?: number;
  accountId: number;
  accountName?: string;
  description?: string | null;
  amount: number;
  isEdited: boolean;
  sortOrder: number;
}

export interface CashVoucher {
  id: number;
  voucherNumber: string;
  voucherType: 'ReceiptVoucher' | 'PaymentVoucher';
  date: string;
  description?: string | null;
  ratesAdded: boolean;
  partyAccountId: number;
  partyAccountName: string;
  totalAmount: number;
  lines: CashVoucherLine[];
}

export interface CreateCashVoucherRequest {
  date: Date;
  partyAccountId: number;
  description?: string | null;
  lines: CreateCashVoucherLineRequest[];
}

export interface CreateCashVoucherLineRequest {
  accountId: number;
  description?: string | null;
  amount: number;
  sortOrder: number;
}
