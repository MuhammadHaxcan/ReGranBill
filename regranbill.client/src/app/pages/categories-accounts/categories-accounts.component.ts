import { Component, OnInit, HostListener } from '@angular/core';
import { CategoryService } from '../../services/category.service';
import { AccountService } from '../../services/account.service';
import { Category } from '../../models/category.model';
import { Account, AccountType } from '../../models/account.model';

@Component({
  selector: 'app-categories-accounts',
  templateUrl: './categories-accounts.component.html',
  styleUrl: './categories-accounts.component.css',
  standalone: false
})
export class CategoriesAccountsComponent implements OnInit {
  activeTab: 'categories' | 'accounts' = 'categories';

  // --- Categories ---
  categories: Category[] = [];
  catSearch = '';
  showCatForm = false;
  catEditingId: number | null = null;
  catFormName = '';
  catFormError = '';

  // --- Accounts ---
  accounts: Account[] = [];
  acctSearch = '';
  showAcctModal = false;
  acctEditingId: number | null = null;
  acctFormError = '';

  // Account form fields
  acctName = '';
  acctCategoryId: number | null = null;
  acctType: AccountType | null = null;

  // Product fields
  acctPacking = '';
  acctPackingWeight: number | null = null;
  acctUnit = 'kg';

  // Expense fields
  acctExpenseNature = '';
  acctBudgetLimit: number | null = null;

  // Account (bank/cash) fields
  acctAccountNumber = '';
  acctOpeningBalance: number | null = null;
  acctBankName = '';

  accountTypes: AccountType[] = ['Product', 'Expense', 'Account'];

  constructor(
    private categoryService: CategoryService,
    private accountService: AccountService
  ) {}

  ngOnInit(): void {
    this.loadCategories();
    this.loadAccounts();
  }

  // Close modal on Escape
  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.showAcctModal) this.closeAcctModal();
    if (this.showCatForm) this.cancelCatForm();
  }

  // ==================== CATEGORIES ====================
  loadCategories(): void {
    this.categories = this.categoryService.getAll();
  }

  get filteredCategories(): Category[] {
    if (!this.catSearch.trim()) return this.categories;
    const term = this.catSearch.toLowerCase();
    return this.categories.filter(c => c.name.toLowerCase().includes(term));
  }

  openAddCatForm(): void {
    this.catEditingId = null;
    this.catFormName = '';
    this.catFormError = '';
    this.showCatForm = true;
  }

  openEditCatForm(cat: Category): void {
    this.catEditingId = cat.id;
    this.catFormName = cat.name;
    this.catFormError = '';
    this.showCatForm = true;
  }

  cancelCatForm(): void {
    this.showCatForm = false;
    this.catFormName = '';
    this.catFormError = '';
    this.catEditingId = null;
  }

  saveCatForm(): void {
    const name = this.catFormName.trim();
    if (!name) { this.catFormError = 'Name is required.'; return; }

    if (this.catEditingId !== null) {
      if (this.categoryService.isDuplicate(name, this.catEditingId)) {
        this.catFormError = 'A category with this name already exists.';
        return;
      }
      this.categoryService.update(this.catEditingId, name);
    } else {
      if (this.categoryService.isDuplicate(name)) {
        this.catFormError = 'A category with this name already exists.';
        return;
      }
      this.categoryService.add(name);
    }
    this.loadCategories();
    this.cancelCatForm();
  }

  deleteCategory(cat: Category): void {
    if (confirm(`Delete category "${cat.name}"?`)) {
      this.categoryService.delete(cat.id);
      this.loadCategories();
    }
  }

  getCategoryName(id: number): string {
    const cat = this.categories.find(c => c.id === id);
    return cat ? cat.name : '—';
  }

  // ==================== ACCOUNTS ====================
  loadAccounts(): void {
    this.accounts = this.accountService.getAll();
  }

  get filteredAccounts(): Account[] {
    if (!this.acctSearch.trim()) return this.accounts;
    const term = this.acctSearch.toLowerCase();
    return this.accounts.filter(a =>
      a.name.toLowerCase().includes(term) ||
      a.accountType.toLowerCase().includes(term)
    );
  }

  openAddAcctModal(): void {
    this.acctEditingId = null;
    this.resetAcctForm();
    this.showAcctModal = true;
  }

  openEditAcctModal(acct: Account): void {
    this.acctEditingId = acct.id;
    this.acctName = acct.name;
    this.acctCategoryId = acct.categoryId;
    this.acctType = acct.accountType;
    this.acctFormError = '';

    // Populate type-specific fields
    this.acctPacking = acct.packing || '';
    this.acctPackingWeight = acct.packingWeightKg ?? null;
    this.acctUnit = acct.unit || 'kg';
    this.acctExpenseNature = acct.expenseNature || '';
    this.acctBudgetLimit = acct.budgetLimit ?? null;
    this.acctAccountNumber = acct.accountNumber || '';
    this.acctOpeningBalance = acct.openingBalance ?? null;
    this.acctBankName = acct.bankName || '';

    this.showAcctModal = true;
  }

  closeAcctModal(): void {
    this.showAcctModal = false;
    this.resetAcctForm();
  }

  resetAcctForm(): void {
    this.acctName = '';
    this.acctCategoryId = null;
    this.acctType = null;
    this.acctFormError = '';
    this.acctPacking = '';
    this.acctPackingWeight = null;
    this.acctUnit = 'kg';
    this.acctExpenseNature = '';
    this.acctBudgetLimit = null;
    this.acctAccountNumber = '';
    this.acctOpeningBalance = null;
    this.acctBankName = '';
  }

  onAcctTypeChange(): void {
    // Reset type-specific fields when type changes
    this.acctPacking = '';
    this.acctPackingWeight = null;
    this.acctUnit = 'kg';
    this.acctExpenseNature = '';
    this.acctBudgetLimit = null;
    this.acctAccountNumber = '';
    this.acctOpeningBalance = null;
    this.acctBankName = '';
  }

  saveAcctForm(): void {
    const name = this.acctName.trim();
    if (!name) { this.acctFormError = 'Account name is required.'; return; }
    if (this.acctCategoryId === null) { this.acctFormError = 'Please select a category.'; return; }
    if (this.acctType === null) { this.acctFormError = 'Please select an account type.'; return; }

    if (this.accountService.isDuplicate(name, this.acctEditingId ?? undefined)) {
      this.acctFormError = 'An account with this name already exists.';
      return;
    }

    const data: Omit<Account, 'id'> = {
      name,
      categoryId: this.acctCategoryId,
      accountType: this.acctType,
    };

    // Attach type-specific fields
    if (this.acctType === 'Product') {
      data.packing = this.acctPacking;
      data.packingWeightKg = this.acctPackingWeight ?? 0;
      data.unit = this.acctUnit;
    } else if (this.acctType === 'Expense') {
      data.expenseNature = this.acctExpenseNature;
      data.budgetLimit = this.acctBudgetLimit ?? 0;
    } else if (this.acctType === 'Account') {
      data.accountNumber = this.acctAccountNumber;
      data.openingBalance = this.acctOpeningBalance ?? 0;
      data.bankName = this.acctBankName;
    }

    if (this.acctEditingId !== null) {
      this.accountService.update(this.acctEditingId, data);
    } else {
      this.accountService.add(data);
    }

    this.loadAccounts();
    this.closeAcctModal();
  }

  deleteAccount(acct: Account): void {
    if (confirm(`Delete account "${acct.name}"?`)) {
      this.accountService.delete(acct.id);
      this.loadAccounts();
    }
  }

  getAcctTypeBadgeClass(type: AccountType): string {
    switch (type) {
      case 'Product': return 'badge-product';
      case 'Expense': return 'badge-expense';
      case 'Account': return 'badge-account';
    }
  }
}
