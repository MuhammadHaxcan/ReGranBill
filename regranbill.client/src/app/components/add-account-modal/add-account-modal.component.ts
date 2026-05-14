import {
  Component, Input, Output, EventEmitter, OnChanges, SimpleChanges, OnInit,
  ViewChild, ElementRef
} from '@angular/core';
import { CategoryService } from '../../services/category.service';
import { AccountService } from '../../services/account.service';
import { ToastService } from '../../services/toast.service';
import { Account, AccountType, PartyRole } from '../../models/account.model';
import { SelectOption } from '../searchable-select/searchable-select.component';
import { getApiErrorMessage } from '../../utils/api-error';

@Component({
  selector: 'app-add-account-modal',
  templateUrl: './add-account-modal.component.html',
  styleUrl: './add-account-modal.component.css',
  standalone: false
})
export class AddAccountModalComponent implements OnInit, OnChanges {
  @Input() isVisible = false;
  @Input() prefillName = '';
  @Input() defaultType?: AccountType;
  @Input() allowedTypes?: AccountType[];
  @Input() defaultPartyRole?: PartyRole;
  @Input() defaultCategoryId?: number;

  @Output() accountCreated = new EventEmitter<Account>();
  @Output() modalClosed = new EventEmitter<void>();

  @ViewChild('nameInput') nameInputRef!: ElementRef<HTMLInputElement>;

  acctName = '';
  acctCategoryId: number | null = null;
  acctType: AccountType | null = null;
  acctPacking = '';
  acctPackingWeight: number | null = null;
  acctAccountNumber = '';
  acctBankName = '';
  acctPartyRole: PartyRole = PartyRole.Customer;
  acctContactPerson = '';
  acctPhone = '';
  acctCity = '';
  acctAddress = '';
  acctFormError = '';
  saving = false;

  categoryOptions: SelectOption[] = [];
  readonly allAccountTypes: AccountType[] = [
    AccountType.Product, AccountType.RawMaterial,
    AccountType.Expense, AccountType.Account, AccountType.Party, AccountType.UnwashedMaterial
  ];
  partyRoles: PartyRole[] = [
    PartyRole.Customer, PartyRole.Vendor, PartyRole.Transporter, PartyRole.Both
  ];

  get visibleAccountTypes(): AccountType[] {
    if (this.allowedTypes?.length) return this.allowedTypes;
    if (!this.defaultType) return this.allAccountTypes;
    if (this.defaultType === AccountType.Product || this.defaultType === AccountType.RawMaterial) {
      return [AccountType.Product, AccountType.RawMaterial, AccountType.UnwashedMaterial];
    }
    return [this.defaultType];
  }

  constructor(
    private categoryService: CategoryService,
    private accountService: AccountService,
    private toast: ToastService
  ) {}

  ngOnInit(): void {
    this.categoryService.getAll().subscribe({
      next: cats => {
        this.categoryOptions = cats.map(c => ({ value: c.id, label: c.name }));
      },
      error: () => this.toast.error('Unable to load categories.')
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['isVisible'] && this.isVisible) {
      this.resetForm();
      this.acctName = this.prefillName;
      this.acctCategoryId = this.defaultCategoryId ?? null;
      // Auto-select type when only one option is available
      if (this.visibleAccountTypes.length === 1) {
        this.acctType = this.visibleAccountTypes[0];
      } else {
        this.acctType = this.defaultType ?? null;
      }
      if (this.defaultPartyRole) {
        this.acctPartyRole = this.defaultPartyRole;
      }
      setTimeout(() => this.nameInputRef?.nativeElement.focus());
    }
  }

  close(): void {
    this.modalClosed.emit();
  }

  onAcctTypeChange(): void {
    this.acctPacking = '';
    this.acctPackingWeight = null;
    this.acctAccountNumber = '';
    this.acctBankName = '';
    this.acctPartyRole = this.defaultPartyRole ?? PartyRole.Customer;
    this.acctContactPerson = '';
    this.acctPhone = '';
    this.acctCity = '';
    this.acctAddress = '';
  }

  save(): void {
    const name = this.acctName.trim();
    if (!name) { this.acctFormError = 'Account name is required.'; return; }
    if (this.acctCategoryId === null) { this.acctFormError = 'Please select a category.'; return; }
    if (this.acctType === null) { this.acctFormError = 'Please select an account type.'; return; }

    const data: any = {
      name,
      categoryId: this.acctCategoryId,
      accountType: this.acctType,
    };

    if (this.acctType === AccountType.Product || this.acctType === AccountType.RawMaterial) {
      data.packing = this.acctPacking;
      data.packingWeightKg = this.acctPackingWeight ?? 0;
    } else if (this.acctType === AccountType.UnwashedMaterial) {
      data.packing = this.acctPacking;
      data.packingWeightKg = this.acctPackingWeight ?? 0;
    } else if (this.acctType === AccountType.Account) {
      data.accountNumber = this.acctAccountNumber;
      data.bankName = this.acctBankName;
    } else if (this.acctType === AccountType.Party) {
      data.partyRole = this.acctPartyRole;
      data.contactPerson = this.acctContactPerson;
      data.phone = this.acctPhone;
      data.city = this.acctCity;
      data.address = this.acctAddress;
    }

    this.saving = true;
    this.acctFormError = '';
    this.accountService.add(data).subscribe({
      next: (created: Account) => {
        this.saving = false;
        this.toast.success(`Account "${created.name}" created.`);
        this.accountCreated.emit(created);
        this.close();
      },
      error: err => {
        this.saving = false;
        this.acctFormError = getApiErrorMessage(err, 'Unable to create account.');
      }
    });
  }

  private resetForm(): void {
    this.acctName = '';
    this.acctCategoryId = null;
    this.acctType = null;
    this.acctFormError = '';
    this.saving = false;
    this.acctPacking = '';
    this.acctPackingWeight = null;
    this.acctAccountNumber = '';
    this.acctBankName = '';
    this.acctPartyRole = PartyRole.Customer;
    this.acctContactPerson = '';
    this.acctPhone = '';
    this.acctCity = '';
    this.acctAddress = '';
  }
}
