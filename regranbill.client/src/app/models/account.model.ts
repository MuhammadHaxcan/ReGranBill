export enum AccountType {
  Product = 'Product',
  RawMaterial = 'RawMaterial',
  Expense = 'Expense',
  Account = 'Account',
  Party = 'Party',
  UnwashedMaterial = 'UnwashedMaterial',
}

export enum PartyRole {
  Customer = 'Customer',
  Vendor = 'Vendor',
  Transporter = 'Transporter',
  Both = 'Both',
}

export interface Account {
  id: number;
  name: string;
  categoryId: number;
  accountType: AccountType;

  // Product-specific
  packing?: string;
  packingWeightKg?: number;

  // Account-specific (bank / cash)
  accountNumber?: string;
  bankName?: string;

  // Party-specific (customer / vendor / transporter)
  partyRole?: PartyRole;
  contactPerson?: string;
  phone?: string;
  city?: string;
  address?: string;
}
