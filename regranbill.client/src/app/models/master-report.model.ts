export interface MasterReportEntry {
  entryId: number;
  voucherNumber: string;
  voucherType: string;
  date: string;
  description: string;
  accountName: string;
  quantity: number | null;
  rate: number | null;
  debit: number;
  credit: number;
  runningBalance: number;
}

export interface MasterReport {
  totalEntries: number;
  totalDebit: number;
  totalCredit: number;
  netBalance: number;
  entries: MasterReportEntry[];
}
