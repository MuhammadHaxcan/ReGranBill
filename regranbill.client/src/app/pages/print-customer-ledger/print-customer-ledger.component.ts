import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-print-customer-ledger',
  templateUrl: './print-customer-ledger.component.html',
  styleUrl: './print-customer-ledger.component.css',
  standalone: false
})
export class PrintCustomerLedgerComponent implements OnInit {
  pdfUrl: SafeResourceUrl | null = null;
  loading = true;
  error = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private sanitizer: DomSanitizer,
    private authService: AuthService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    const accountId = this.route.snapshot.paramMap.get('accountId');
    if (!accountId) {
      this.error = 'No account ID provided';
      this.loading = false;
      return;
    }

    const token = this.authService.currentUser?.token;
    if (!token) {
      this.authService.logout();
      this.router.navigate(['/login']);
      return;
    }

    const fromDate = this.route.snapshot.queryParamMap.get('fromDate');
    const toDate = this.route.snapshot.queryParamMap.get('toDate');

    if (!fromDate || !toDate) {
      this.error = 'Date range is required';
      this.loading = false;
      return;
    }

    const url = `/api/customer-ledger/${accountId}/pdf?fromDate=${fromDate}&toDate=${toDate}`;

    const xhr = new XMLHttpRequest();
    xhr.open('GET', url, true);
    xhr.setRequestHeader('Authorization', `Bearer ${token}`);
    xhr.responseType = 'blob';

    xhr.onload = () => {
      if (xhr.status === 200) {
        const blob = xhr.response;
        const objectUrl = URL.createObjectURL(blob);
        this.pdfUrl = this.sanitizer.bypassSecurityTrustResourceUrl(objectUrl);
        document.title = `Print Ledger - ${accountId}`;
        this.cdr.detectChanges();
      } else if (xhr.status === 401 || xhr.status === 403) {
        this.authService.logout();
        this.router.navigate(['/login']);
      } else {
        this.error = `Failed to load ledger PDF (${xhr.status})`;
        this.loading = false;
        this.cdr.detectChanges();
      }
    };

    xhr.onerror = () => {
      this.error = 'Network error loading ledger PDF';
      this.loading = false;
      this.cdr.detectChanges();
    };

    xhr.send();
  }

  onIframeLoad(): void {
    this.loading = false;
    this.cdr.detectChanges();
  }
}