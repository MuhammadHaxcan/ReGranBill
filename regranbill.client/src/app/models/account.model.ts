export enum AccountType {
  Product = 'Product',
  Expense = 'Expense',
  Account = 'Account',
  Party = 'Party',
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
  unit?: string;

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
