export type AccountType = 'Product' | 'Expense' | 'Account';

export interface Account {
  id: number;
  name: string;
  categoryId: number;
  accountType: AccountType;

  // Product-specific
  packing?: string;
  packingWeightKg?: number;
  unit?: string;

  // Expense-specific
  expenseNature?: string;
  budgetLimit?: number;

  // Account-specific (bank / cash / receivable)
  accountNumber?: string;
  openingBalance?: number;
  bankName?: string;
}
