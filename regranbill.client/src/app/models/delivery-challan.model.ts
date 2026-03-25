export enum VoucherType {
  SaleVoucher = 'SaleVoucher',
  PurchaseVoucher = 'PurchaseVoucher',
  JournalVoucher = 'JournalVoucher',
  CartageVoucher = 'CartageVoucher',
}

export interface Product {
  id: number;
  name: string;
  packing: string;
  packingWeightKg: number;
}

export interface ProductLine {
  id?: number;
  product: Product | null;
  rbp: 'Yes' | 'No';
  qty: number;
  rate: number;
  sortOrder?: number;
}

export interface Cartage {
  transporterId: number;
  transporterName: string;
  city: string;
  amount: number;
}

export interface Customer {
  id: number;
  name: string;
  city: string;
}

export interface JournalEntry {
  id: number;
  accountId: number;
  accountName: string;
  description: string;
  debit: number;
  credit: number;
  qty?: number;
  rbp?: string;
  rate?: number;
  isEdited: boolean;
  sortOrder: number;
}

export interface JournalVoucherSummary {
  id: number;
  voucherNumber: string;
  voucherType: string;
  ratesAdded: boolean;
  totalDebit: number;
  totalCredit: number;
  entries: JournalEntry[];
}

export interface DeliveryChallan {
  id?: number;
  dcNumber: string;
  date: Date | string;
  customerId: number | null;
  customerName?: string;
  vehicleNumber?: string;
  description: string;
  voucherType: VoucherType;
  lines: ProductLine[];
  cartage: Cartage | null;
  ratesAdded: boolean;
  journalVouchers: JournalVoucherSummary[];
}

export interface DeliveryChallanLineViewModel {
  id: number;
  productId: number;
  productName: string;
  packing: string;
  packingWeightKg: number;
  rbp: 'Yes' | 'No';
  qty: number;
  rate: number;
  sortOrder: number;
}

export interface DeliveryCartageViewModel {
  transporterId: number;
  transporterName: string;
  city: string;
  amount: number;
}

export interface DeliveryChallanViewModel {
  id: number;
  dcNumber: string;
  date: string;
  customerId: number;
  customerName: string;
  vehicleNumber?: string | null;
  description: string;
  lines: DeliveryChallanLineViewModel[];
  cartage: DeliveryCartageViewModel | null;
  ratesAdded: boolean;
  journalVouchers: JournalVoucherSummary[];
}

export interface DeliveryChallanUpsertRequest {
  date: Date;
  customerId: number;
  vehicleNumber?: string | null;
  description: string;
  lines: {
    productId: number;
    rbp: 'Yes' | 'No';
    qty: number;
    rate: number;
    sortOrder: number;
  }[];
  cartage: {
    transporterId: number;
    amount: number;
  } | null;
}

export interface UpdateDeliveryRatesRequest {
  lines: {
    entryId: number;
    rate: number;
  }[];
}
