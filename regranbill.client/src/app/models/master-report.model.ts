export interface MasterReportEntry {
  entryId: number;
  voucherId: number;
  voucherNumber: string;
  voucherType: string;
  date: string;
  description?: string | null;
  accountName: string;
  quantity: number | null;
  rate: number | null;
  debit: number;
  credit: number;
  runningBalance: number;
}

export interface MasterReport {
  fromDate?: string | null;
  toDate?: string | null;
  categoryName?: string | null;
  accountName?: string | null;
  totalEntries: number;
  totalDebit: number;
  totalCredit: number;
  netBalance: number;
  entries: MasterReportEntry[];
}
