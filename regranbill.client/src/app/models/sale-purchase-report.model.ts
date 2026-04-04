export type SalePurchaseReportType = 'All' | 'Sale' | 'Purchase';

export interface SalePurchaseReportRow {
  voucherId: number;
  voucherNumber: string;
  voucherType: string;
  date: string;
  productId: number;
  productName: string;
  packing: string | null;
  unit: string | null;
  rbp: string;
  qty: number;
  packingWeightKg: number;
  totalWeightKg: number;
  displayQuantity: string;
  fromName: string;
  toName: string;
  transporterName: string | null;
  groupLabel: string;
  groupSortDate: string;
}

export interface SalePurchaseReport {
  totalRows: number;
  totalSaleRows: number;
  totalPurchaseRows: number;
  totalPackedBags: number;
  totalWeightKg: number;
  rows: SalePurchaseReportRow[];
}
