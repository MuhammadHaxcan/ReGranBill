export interface CustomerLedgerEntry {
  entryId: number;
  voucherId: number;
  voucherType: string;
  voucherNumber: string;
  date: string;
  description?: string | null;
  productName?: string | null;
  packing?: string | null;
  qty?: number | null;
  weight?: number | null;
  rate?: number | null;
  debit: number;
  credit: number;
  runningBalance: number;
}

export interface CustomerLedger {
  accountId: number;
  accountName: string;
  partyType: string;
  hasOpeningBalance: boolean;
  openingBalance: number;
  closingBalance: number;
  entries: CustomerLedgerEntry[];
}
