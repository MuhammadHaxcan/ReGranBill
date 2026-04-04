export interface AccountClosingSummaryRow {
  accountId: number;
  accountName: string;
  openingBalance: number;
  periodDebit: number;
  periodCredit: number;
  closingBalance: number;
}

export interface AccountClosingHistoryEntry {
  entryId: number;
  voucherId: number;
  voucherNumber: string;
  voucherType: string;
  date: string;
  description: string | null;
  quantity: number | null;
  rate: number | null;
  debit: number;
  credit: number;
  runningBalance: number;
}

export interface AccountClosingReport {
  fromDate: string | null;
  toDate: string | null;
  selectedAccountId: number | null;
  selectedAccountName?: string | null;
  historyAccountId?: number | null;
  historyAccountName?: string | null;
  totalAccounts: number;
  totalOpeningBalance: number;
  totalDebit: number;
  totalCredit: number;
  totalClosingBalance: number;
  accounts: AccountClosingSummaryRow[];
  history: AccountClosingHistoryEntry[];
}
