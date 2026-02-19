import { Injectable } from '@angular/core';
import { Account, AccountType } from '../models/account.model';

@Injectable({
  providedIn: 'root'
})
export class AccountService {
  private nextId = 8;

  private accounts: Account[] = [
    { id: 1, name: 'HDPE Blue Drum', categoryId: 2, accountType: 'Product', packing: '50kg / Bag', packingWeightKg: 50, unit: 'kg' },
    { id: 2, name: 'PP-125 Natural', categoryId: 2, accountType: 'Product', packing: '25kg / Bag', packingWeightKg: 25, unit: 'kg' },
    { id: 3, name: 'Electricity', categoryId: 6, accountType: 'Expense', expenseNature: 'Monthly', budgetLimit: 50000 },
    { id: 4, name: 'Diesel Fuel', categoryId: 4, accountType: 'Expense', expenseNature: 'Variable', budgetLimit: 30000 },
    { id: 5, name: 'HBL Current Account', categoryId: 1, accountType: 'Account', accountNumber: '1234-5678-0001', openingBalance: 250000, bankName: 'Habib Bank Ltd' },
    { id: 6, name: 'Cash In Hand', categoryId: 1, accountType: 'Account', accountNumber: '', openingBalance: 45000, bankName: '' },
    { id: 7, name: 'LDPE Film Grade', categoryId: 2, accountType: 'Product', packing: '25kg / Bag', packingWeightKg: 25, unit: 'kg' },
  ];

  getAll(): Account[] {
    return [...this.accounts];
  }

  add(account: Omit<Account, 'id'>): Account {
    const newAccount: Account = { ...account, id: this.nextId++ };
    this.accounts.push(newAccount);
    return newAccount;
  }

  update(id: number, data: Partial<Account>): boolean {
    const acc = this.accounts.find(a => a.id === id);
    if (!acc) return false;
    Object.assign(acc, data);
    return true;
  }

  delete(id: number): boolean {
    const index = this.accounts.findIndex(a => a.id === id);
    if (index === -1) return false;
    this.accounts.splice(index, 1);
    return true;
  }

  isDuplicate(name: string, excludeId?: number): boolean {
    return this.accounts.some(
      a => a.name.toLowerCase() === name.toLowerCase() && a.id !== excludeId
    );
  }
}
