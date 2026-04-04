export interface StatementEntry {
  entryId: number;
  voucherId: number;
  date: string;
  voucherNumber: string;
  voucherType: string;
  description?: string | null;
  debit: number;
  credit: number;
  runningBalance: number;
}

export interface StatementOfAccount {
  accountId: number;
  accountName: string;
  partyRole?: string | null;
  contactPerson?: string | null;
  phone?: string | null;
  city?: string | null;
  address?: string | null;
  fromDate: string | null;
  toDate: string | null;
  totalDebit: number;
  totalCredit: number;
  netBalance: number;
  entries: StatementEntry[];
}
