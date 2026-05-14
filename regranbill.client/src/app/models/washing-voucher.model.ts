export interface CreateWashingVoucherRequest {
  date: string;
  description: string | null;
  sourceVendorId: number;
  unwashedAccountId: number;
  inputWeightKg: number;
  outputWeightKg: number;
  outputLines: CreateWashingVoucherOutputLineRequest[];
  thresholdPct: number;
}

export interface CreateWashingVoucherOutputLineRequest {
  accountId: number;
  weightKg: number;
}

export interface WashingVoucherDto {
  id: number;
  voucherNumber: string;
  date: string;
  description?: string | null;
  sourceVendorId: number;
  sourceVendorName?: string | null;
  unwashedAccountId: number;
  unwashedAccountName?: string | null;
  washedAccountId?: number | null;
  washedAccountName?: string | null;
  inputWeightKg: number;
  outputWeightKg: number;
  outputLines: WashingVoucherOutputLineDto[];
  wastageKg: number;
  wastagePct: number;
  sourceRate: number;
  inputCost: number;
  washedDebit: number;
  washedRate: number;
  thresholdPct: number;
  excessWastageKg: number;
  excessWastageValue: number;
  createdAt: string;
}

export interface WashingVoucherListDto {
  id: number;
  voucherNumber: string;
  date: string;
  description?: string | null;
  sourceVendorId: number;
  sourceVendorName?: string | null;
  unwashedAccountId: number;
  unwashedAccountName?: string | null;
  inputWeightKg: number;
  outputWeightKg: number;
  outputLineCount: number;
  wastageKg: number;
  wastagePct: number;
  washedDebit: number;
  washedRate: number;
  excessWastageKg: number;
  excessWastageValue: number;
  createdAt: string;
}

export interface LatestUnwashedRateDto {
  accountId: number;
  rate: number;
  sourceVoucherNumber: string;
  sourceDate: string;
}

export interface WashingVoucherOutputLineDto {
  accountId: number;
  accountName: string;
  weightKg: number;
  rate: number;
  debit: number;
}
