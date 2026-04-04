export type VoucherType =
  | 'SaleVoucher'
  | 'PurchaseVoucher'
  | 'JournalVoucher'
  | 'ReceiptVoucher'
  | 'PaymentVoucher'
  | 'CartageVoucher';

export interface VoucherEditorEntry {
  id: number;
  accountId: number;
  accountName?: string;
  description?: string;
  debit: number;
  credit: number;
  qty?: number | null;
  totalWeightKg?: number | null;
  rbp?: string | null;
  rate?: number | null;
  isEdited: boolean;
  sortOrder: number;
}

export interface VoucherEditorVoucher {
  id: number;
  voucherNumber: string;
  voucherType: VoucherType;
  date: string;
  description?: string;
  vehicleNumber?: string | null;
  ratesAdded: boolean;
  totalDebit: number;
  totalCredit: number;
  entries: VoucherEditorEntry[];
}

export interface UpdateVoucherEditorRequest {
  voucherType: VoucherType;
  voucherNumber: string;
  date: Date;
  description?: string | null;
  vehicleNumber?: string | null;
  entries: UpdateVoucherEditorEntryRequest[];
}

export interface UpdateVoucherEditorEntryRequest {
  accountId: number;
  description?: string | null;
  debit: number;
  credit: number;
  qty?: number | null;
  totalWeightKg?: number | null;
  rbp?: string | null;
  rate?: number | null;
  sortOrder: number;
}
