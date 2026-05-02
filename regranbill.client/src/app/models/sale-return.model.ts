import { DeliveryChallanLineViewModel, JournalVoucherSummary } from './delivery-challan.model';
export type { JournalVoucherSummary };

export interface SaleReturnLine {
  id?: number;
  product: Product | null;
  rbp: 'Yes' | 'No';
  qty: number;
  rate: number;
  sortOrder?: number;
}

export interface Product {
  id: number;
  name: string;
  packing: string;
  packingWeightKg: number;
}

export interface SaleReturnViewModel {
  id: number;
  srNumber: string;
  date: string;
  customerId: number;
  customerName?: string | null;
  vehicleNumber?: string | null;
  description?: string | null;
  voucherType: string;
  ratesAdded: boolean;
  lines: DeliveryChallanLineViewModel[];
  journalVouchers: JournalVoucherSummary[];
}

export interface SaleReturnLineRequest {
  productId: number;
  rbp: 'Yes' | 'No';
  qty: number;
  rate: number;
  sortOrder: number;
}

export interface SaleReturnUpsertRequest {
  date: string;
  customerId: number;
  vehicleNumber?: string | null;
  description: string;
  lines: SaleReturnLineRequest[];
}

export interface SaleReturnRateUpdateLineRequest {
  entryId: number;
  rate: number;
}

export interface UpdateSaleReturnRatesRequest {
  lines: SaleReturnRateUpdateLineRequest[];
}