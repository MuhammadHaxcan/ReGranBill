export interface JournalVoucherEntry {
  id?: number;
  accountId: number;
  accountName?: string;
  description?: string;
  debit: number;
  credit: number;
  isEdited: boolean;
  sortOrder: number;
}

export interface JournalVoucher {
  id: number;
  voucherNumber: string;
  date: string;
  voucherType: string;
  description?: string;
  ratesAdded: boolean;
  totalDebit: number;
  totalCredit: number;
  entries: JournalVoucherEntry[];
}

export interface CreateJournalVoucherRequest {
  date: Date;
  description?: string | null;
  entries: CreateJournalVoucherEntryRequest[];
}

export interface CreateJournalVoucherEntryRequest {
  accountId: number;
  description?: string | null;
  debit: number;
  credit: number;
  sortOrder: number;
}
