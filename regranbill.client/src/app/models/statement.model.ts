export interface StatementEntry {
  entryId: number;
  voucherId: number;
  date: string;
  voucherNumber: string;
  voucherType: string;
  description: string;
  debit: number;
  credit: number;
  runningBalance: number;
}

export interface StatementOfAccount {
  accountId: number;
  accountName: string;
  partyRole?: string | null;
  contactPerson: string;
  phone: string;
  city: string;
  address: string;
  fromDate: string | null;
  toDate: string | null;
  totalDebit: number;
  totalCredit: number;
  netBalance: number;
  entries: StatementEntry[];
}
