export interface PurchaseVoucherProductLine {
  id: number;
  productId: number;
  productName?: string | null;
  packing?: string | null;
  packingWeightKg: number;
  qty: number;
  totalWeightKg: number;
  avgWeightPerBagKg: number;
  rate: number;
  sortOrder: number;
}

export interface PurchaseVoucherCartage {
  transporterId: number;
  transporterName?: string | null;
  city?: string | null;
  amount: number;
}

export interface PurchaseVoucherJournalEntry {
  id: number;
  accountId: number;
  accountName?: string | null;
  description?: string | null;
  debit: number;
  credit: number;
  qty?: number | null;
  rbp?: string | null;
  rate?: number | null;
  isEdited: boolean;
  sortOrder: number;
}

export interface PurchaseVoucherJournalSummary {
  id: number;
  voucherNumber: string;
  voucherType: string;
  ratesAdded: boolean;
  totalDebit: number;
  totalCredit: number;
  entries: PurchaseVoucherJournalEntry[];
}

export interface PurchaseVoucherApiDto {
  id: number;
  voucherNumber: string;
  date: string;
  vendorId: number;
  vendorName?: string | null;
  vehicleNumber?: string | null;
  description?: string | null;
  voucherType: string;
  ratesAdded: boolean;
  lines: PurchaseVoucherProductLine[];
  cartage: PurchaseVoucherCartage | null;
  journalVouchers: PurchaseVoucherJournalSummary[];
}

export interface PurchaseVoucherViewModel {
  id: number;
  voucherNumber: string;
  date: string;
  vendorId: number;
  vendorName?: string | null;
  vehicleNumber?: string | null;
  description?: string | null;
  voucherType: string;
  ratesAdded: boolean;
  lines: PurchaseVoucherProductLine[];
  cartage: PurchaseVoucherCartage | null;
  journalVouchers: PurchaseVoucherJournalSummary[];
}

export interface PurchaseVoucherLineRequest {
  productId: number;
  qty: number;
  totalWeightKg: number;
  rate: number;
  sortOrder: number;
}

export interface PurchaseVoucherCartageRequest {
  transporterId: number;
  amount: number;
}

export interface PurchaseVoucherUpsertRequest {
  date: Date;
  vendorId: number;
  vehicleNumber: string | null;
  description: string;
  lines: PurchaseVoucherLineRequest[];
  cartage: PurchaseVoucherCartageRequest | null;
}

export interface PurchaseVoucherRateUpdateLineRequest {
  entryId: number;
  rate: number;
}

export interface PurchaseVoucherRateUpdateRequest {
  lines: PurchaseVoucherRateUpdateLineRequest[];
}
