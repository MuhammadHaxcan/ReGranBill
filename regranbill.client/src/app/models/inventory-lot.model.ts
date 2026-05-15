export interface AvailableInventoryLot {
  lotId: number;
  lotNumber: string;
  productAccountId: number;
  productAccountName: string;
  vendorId?: number | null;
  vendorName?: string | null;
  sourceVoucherNumber: string;
  sourceVoucherType: string;
  sourceDate: string;
  rate: number;
  originalQty?: number | null;
  originalWeightKg: number;
  availableWeightKg: number;
}
