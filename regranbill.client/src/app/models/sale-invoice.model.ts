export interface Product {
  id: number;
  name: string;
  packing: string;
  packingWeightKg: number;
}

export interface ProductLine {
  product: Product | null;
  rbp: 'Yes' | 'No';
  qty: number;
  rate: number;
}

export interface Cartage {
  providerName: string;
  city: string;
  amount: number;
}

export interface Customer {
  id: number;
  name: string;
  city: string;
}

export type InvoiceStatus = 'Draft' | 'Posted';

export interface SaleInvoice {
  invoiceNumber: string;
  date: Date;
  customerId: number | null;
  description: string;
  lines: ProductLine[];
  cartage: Cartage | null;
  status: InvoiceStatus;
}
