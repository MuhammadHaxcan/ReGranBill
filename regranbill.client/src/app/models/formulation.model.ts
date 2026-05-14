import { ProductionLineKind } from './production-voucher.model';

export interface FormulationLineDto {
  id: number;
  lineKind: ProductionLineKind;
  accountId: number;
  accountName?: string | null;
  amountPerBase: number;
  bagsPerBase?: number | null;
  sortOrder: number;
}

export interface FormulationDto {
  id: number;
  name: string;
  description?: string | null;
  baseInputKg: number;
  isActive: boolean;
  lines: FormulationLineDto[];
  createdAt: string;
  updatedAt: string;
}

export interface FormulationLineRequest {
  lineKind: ProductionLineKind;
  accountId: number;
  amountPerBase: number;
  bagsPerBase?: number | null;
  sortOrder: number;
}

export interface CreateFormulationRequest {
  name: string;
  description?: string | null;
  baseInputKg: number;
  isActive: boolean;
  lines: FormulationLineRequest[];
}

export interface AppliedLineDto {
  accountId: number;
  accountName?: string | null;
  qty: number;
  weightKg: number;
  sortOrder: number;
}

export interface AppliedShortageDto {
  accountId: number;
  accountName?: string | null;
  weightKg: number;
}

export interface ApplyFormulationResponse {
  inputs: AppliedLineDto[];
  outputs: AppliedLineDto[];
  byproducts: AppliedLineDto[];
  shortage: AppliedShortageDto | null;
}
