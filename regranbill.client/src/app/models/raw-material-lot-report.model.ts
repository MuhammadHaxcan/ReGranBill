export interface RawMaterialLotReportFilters {
  from?: string;
  to?: string;
  vendorId?: number;
  productId?: number;
  lotNumber?: string;
  openOnly?: boolean;
  includeDetails?: boolean;
}

export interface RawMaterialLotReport {
  from?: string | null;
  to?: string | null;
  vendorId?: number | null;
  productId?: number | null;
  lotNumber?: string | null;
  openOnly: boolean;
  lots: RawMaterialLotRow[];
  movements: RawMaterialLotMovement[];
}

export interface RawMaterialLotRow {
  lotId: number;
  lotNumber: string;
  productId: number;
  productName: string;
  vendorId?: number | null;
  vendorName?: string | null;
  sourceVoucherNumber: string;
  sourceDate: string;
  originalQty?: number | null;
  originalWeightKg: number;
  consumedWeightKg: number;
  availableWeightKg: number;
  baseRate: number;
  status: string;
}

export interface RawMaterialLotMovement {
  lotId: number;
  lotNumber: string;
  transactionId: number;
  voucherNumber: string;
  voucherType: string;
  transactionType: string;
  transactionDate: string;
  weightKgIn: number;
  weightKgOut: number;
  runningAvailableKg: number;
  rate: number;
  valueDelta: number;
  notes?: string | null;
}
