import { Component, OnInit, HostListener, ChangeDetectorRef } from '@angular/core';
import { CategoryService } from '../../services/category.service';
import { AccountService } from '../../services/account.service';
import { Category } from '../../models/category.model';
import { Account, AccountType, PartyRole } from '../../models/account.model';
import { SelectOption } from '../../components/searchable-select/searchable-select.component';

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

  // Account (bank/cash) fields
  acctAccountNumber = '';
  acctBankName = '';

  // Party fields
  acctPartyRole: PartyRole = PartyRole.Customer;
  acctContactPerson = '';
  acctPhone = '';
  acctCity = '';
  acctAddress = '';

  categoryOptions: SelectOption[] = [];
  accountTypes: AccountType[] = [AccountType.Product, AccountType.Expense, AccountType.Account, AccountType.Party];
  partyRoles: PartyRole[] = [PartyRole.Customer, PartyRole.Vendor, PartyRole.Transporter, PartyRole.Both];

  constructor(
    private categoryService: CategoryService,
    private accountService: AccountService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadCategories();
    this.loadAccounts();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.showAcctModal) this.closeAcctModal();
    if (this.showCatForm) this.cancelCatForm();
  }

  // ==================== CATEGORIES ====================
  loadCategories(): void {
    this.categoryService.getAll().subscribe(cats => {
      this.categories = cats;
      this.categoryOptions = cats.map(c => ({ value: c.id, label: c.name }));
      this.cdr.detectChanges();
    });
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

    const isDuplicate = this.categories.some(
      c => c.name.toLowerCase() === name.toLowerCase() && c.id !== this.catEditingId
    );
    if (isDuplicate) {
      this.catFormError = 'A category with this name already exists.';
      return;
    }

    if (this.catEditingId !== null) {
      this.categoryService.update(this.catEditingId, name).subscribe(() => {
        this.loadCategories();
        this.cancelCatForm();
        this.cdr.detectChanges();
      });
    } else {
      this.categoryService.add(name).subscribe(() => {
        this.loadCategories();
        this.cancelCatForm();
        this.cdr.detectChanges();
      });
    }
  }

  deleteCategory(cat: Category): void {
    if (confirm(`Delete category "${cat.name}"?`)) {
      this.categoryService.delete(cat.id).subscribe(() => {
        this.loadCategories();
      });
    }
  }

  getCategoryName(id: number): string {
    const cat = this.categories.find(c => c.id === id);
    return cat ? cat.name : '—';
  }

  // ==================== ACCOUNTS ====================
  loadAccounts(): void {
    this.accountService.getAll().subscribe(accts => {
      this.accounts = accts;
      this.cdr.detectChanges();
    });
  }

  get filteredAccounts(): Account[] {
    if (!this.acctSearch.trim()) return this.accounts;
    const term = this.acctSearch.toLowerCase();
    return this.accounts.filter(a =>
      a.name.toLowerCase().includes(term) ||
      a.accountType.toLowerCase().includes(term) ||
      (a.partyRole && a.partyRole.toLowerCase().includes(term))
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

    this.acctPacking = acct.packing || '';
    this.acctPackingWeight = acct.packingWeightKg ?? null;
    this.acctUnit = acct.unit || 'kg';
    this.acctAccountNumber = acct.accountNumber || '';
    this.acctBankName = acct.bankName || '';
    this.acctPartyRole = acct.partyRole || PartyRole.Customer;
    this.acctContactPerson = acct.contactPerson || '';
    this.acctPhone = acct.phone || '';
    this.acctCity = acct.city || '';
    this.acctAddress = acct.address || '';

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
    this.acctAccountNumber = '';
    this.acctBankName = '';
    this.acctPartyRole = PartyRole.Customer;
    this.acctContactPerson = '';
    this.acctPhone = '';
    this.acctCity = '';
    this.acctAddress = '';
  }

  onAcctTypeChange(): void {
    this.acctPacking = '';
    this.acctPackingWeight = null;
    this.acctUnit = 'kg';
    this.acctAccountNumber = '';
    this.acctBankName = '';
    this.acctPartyRole = PartyRole.Customer;
    this.acctContactPerson = '';
    this.acctPhone = '';
    this.acctCity = '';
    this.acctAddress = '';
  }

  saveAcctForm(): void {
    const name = this.acctName.trim();
    if (!name) { this.acctFormError = 'Account name is required.'; return; }
    if (this.acctCategoryId === null) { this.acctFormError = 'Please select a category.'; return; }
    if (this.acctType === null) { this.acctFormError = 'Please select an account type.'; return; }

    const isDuplicate = this.accounts.some(
      a => a.name.toLowerCase() === name.toLowerCase() && a.id !== this.acctEditingId
    );
    if (isDuplicate) {
      this.acctFormError = 'An account with this name already exists.';
      return;
    }

    const data: any = {
      name,
      categoryId: this.acctCategoryId,
      accountType: this.acctType,
    };

    if (this.acctType === 'Product') {
      data.packing = this.acctPacking;
      data.packingWeightKg = this.acctPackingWeight ?? 0;
      data.unit = this.acctUnit;
    } else if (this.acctType === 'Account') {
      data.accountNumber = this.acctAccountNumber;
      data.bankName = this.acctBankName;
    } else if (this.acctType === 'Party') {
      data.partyRole = this.acctPartyRole;
      data.contactPerson = this.acctContactPerson;
      data.phone = this.acctPhone;
      data.city = this.acctCity;
      data.address = this.acctAddress;
    }

    if (this.acctEditingId !== null) {
      this.accountService.update(this.acctEditingId, data).subscribe(() => {
        this.loadAccounts();
        this.closeAcctModal();
        this.cdr.detectChanges();
      });
    } else {
      this.accountService.add(data).subscribe(() => {
        this.loadAccounts();
        this.closeAcctModal();
        this.cdr.detectChanges();
      });
    }
  }

  deleteAccount(acct: Account): void {
    if (confirm(`Delete account "${acct.name}"?`)) {
      this.accountService.delete(acct.id).subscribe(() => this.loadAccounts());
    }
  }

  getAcctTypeBadgeClass(type: AccountType): string {
    switch (type) {
      case AccountType.Product: return 'badge-product';
      case AccountType.Expense: return 'badge-expense';
      case AccountType.Account: return 'badge-account';
      case AccountType.Party: return 'badge-party';
      default: return '';
    }
  }
}
