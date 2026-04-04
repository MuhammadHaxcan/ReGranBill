export interface ProductStockReportFilters {
  from?: string;
  to?: string;
  categoryId?: number;
  productId?: number;
  includeDetails?: boolean;
}

export interface ProductStockMetric {
  bags: number;
  kg: number;
  value: number;
}

export interface ProductStockTotals {
  productCount: number;
  anomalyCount: number;
  opening: ProductStockMetric;
  inward: ProductStockMetric;
  outward: ProductStockMetric;
  closing: ProductStockMetric;
}

export interface ProductStockRow {
  productId: number;
  productName: string;
  packing: string | null;
  packingWeightKg: number | null;
  anomalyCount: number;
  opening: ProductStockMetric;
  inward: ProductStockMetric;
  outward: ProductStockMetric;
  closing: ProductStockMetric;
}

export interface ProductStockMovement {
  entryId: number;
  voucherId: number;
  voucherNumber: string;
  voucherType: string;
  date: string;
  productId: number;
  productName: string;
  description: string | null;
  debit: number;
  credit: number;
  qty: number | null;
  rbp: string | null;
  rate: number | null;
  weightKg: number;
  value: number;
  direction: string;
  isEdited: boolean;
  anomalyNote: string | null;
}

export interface ProductStockAnomaly {
  code: string;
  message: string;
  count: number;
}

export interface ProductStockReport {
  from: string | null;
  to: string | null;
  categoryId: number | null;
  productId: number | null;
  includeDetails: boolean;
  totals: ProductStockTotals;
  products: ProductStockRow[];
  movements: ProductStockMovement[];
  anomalies: ProductStockAnomaly[];
}
