import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { StatementService } from '../../services/statement.service';
import { AccountService } from '../../services/account.service';
import { StatementOfAccount } from '../../models/statement.model';
import { Account } from '../../models/account.model';

@Component({
  selector: 'app-soa',
  templateUrl: './soa.component.html',
  styleUrl: './soa.component.css',
  standalone: false
})
export class SoaComponent implements OnInit {
  customers: Account[] = [];
  selectedAccountId: number | null = null;
  fromDate = '';
  toDate = '';
  statement: StatementOfAccount | null = null;
  loading = false;

  constructor(
    private route: ActivatedRoute,
    private statementService: StatementService,
    private accountService: AccountService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.accountService.getCustomers().subscribe(data => {
      this.customers = data;
      this.cdr.detectChanges();
    });

    const paramId = this.route.snapshot.paramMap.get('accountId');
    if (paramId) {
      this.selectedAccountId = +paramId;
      this.loadStatement();
    }
  }

  loadStatement(): void {
    if (!this.selectedAccountId) return;
    this.loading = true;
    this.statementService.getStatement(
      this.selectedAccountId,
      this.fromDate || undefined,
      this.toDate || undefined
    ).subscribe({
      next: data => {
        this.statement = data;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  clearFilters(): void {
    this.fromDate = '';
    this.toDate = '';
    if (this.selectedAccountId) {
      this.loadStatement();
    }
  }

  getFormattedDate(date: string): string {
    return new Date(date).toLocaleDateString('en-GB', {
      day: '2-digit',
      month: 'short',
      year: 'numeric'
    });
  }
}
