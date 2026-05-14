export type ProductionLineKind = 'Input' | 'Output' | 'Byproduct' | 'Shortage';

export interface ProductionLineDto {
  id: number;
  accountId: number;
  accountName?: string | null;
  packing?: string | null;
  packingWeightKg?: number | null;
  qty: number;
  weightKg: number;
  description?: string | null;
  sortOrder: number;
  vendorId?: number | null;
  vendorName?: string | null;
  rate?: number | null;
}

export interface ProductionShortageDto {
  id: number;
  accountId: number;
  accountName?: string | null;
  weightKg: number;
  rate?: number | null;
}

export interface LatestPurchaseRateDto {
  accountId: number;
  rate: number;
  sourceVoucherNumber: string;
  sourceDate: string;
}

export interface ProductionVoucherApiDto {
  id: number;
  voucherNumber: string;
  date: string;
  lotNumber?: string | null;
  description?: string | null;
  voucherType: string;
  formulationId?: number | null;
  inputs: ProductionLineDto[];
  outputs: ProductionLineDto[];
  byproducts: ProductionLineDto[];
  shortage: ProductionShortageDto | null;
  totalInputKg: number;
  totalOutputKg: number;
  totalByproductKg: number;
  shortageKg: number;
  totalInputCost: number;
  derivedOutputRate: number;
}

export interface ProductionVoucherListDto {
  id: number;
  voucherNumber: string;
  date: string;
  description?: string | null;
  lotNumber?: string | null;
  totalInputKg: number;
  totalOutputKg: number;
  totalByproductKg: number;
  shortageKg: number;
  createdAt: string;
}

export interface ProductionLineRequest {
  accountId: number;
  qty: number;
  weightKg: number;
  description?: string | null;
  sortOrder: number;
  vendorId?: number | null;
  rate?: number | null;
}

export interface ProductionShortageRequest {
  accountId: number;
  weightKg: number;
}

export interface ProductionVoucherUpsertRequest {
  date: string;
  lotNumber: string | null;
  description: string | null;
  formulationId: number | null;
  inputs: ProductionLineRequest[];
  outputs: ProductionLineRequest[];
  byproducts: ProductionLineRequest[];
  shortage: ProductionShortageRequest | null;
}
