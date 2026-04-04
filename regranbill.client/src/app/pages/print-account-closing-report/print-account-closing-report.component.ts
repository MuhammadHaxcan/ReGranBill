import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-print-account-closing-report',
  templateUrl: './print-account-closing-report.component.html',
  styleUrl: './print-account-closing-report.component.css',
  standalone: false
})
export class PrintAccountClosingReportComponent implements OnInit {
  pdfUrl: SafeResourceUrl | null = null;
  loading = true;
  error = '';

  constructor(
    private route: ActivatedRoute,
    private sanitizer: DomSanitizer,
    private authService: AuthService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    const token = this.authService.currentUser?.token;
    if (!token) {
      this.error = 'Not authenticated';
      this.loading = false;
      return;
    }

    const query = new URLSearchParams();
    const from = this.route.snapshot.queryParamMap.get('from');
    const to = this.route.snapshot.queryParamMap.get('to');
    const accountId = this.route.snapshot.queryParamMap.get('accountId');
    const historyAccountId = this.route.snapshot.queryParamMap.get('historyAccountId');

    if (from) query.set('from', from);
    if (to) query.set('to', to);
    if (accountId) query.set('accountId', accountId);
    if (historyAccountId) query.set('historyAccountId', historyAccountId);

    const url = query.toString()
      ? `/api/account-closing-report/pdf?${query.toString()}`
      : '/api/account-closing-report/pdf';

    const xhr = new XMLHttpRequest();
    xhr.open('GET', url, true);
    xhr.setRequestHeader('Authorization', `Bearer ${token}`);
    xhr.responseType = 'blob';

    xhr.onload = () => {
      if (xhr.status === 200) {
        const objectUrl = URL.createObjectURL(xhr.response);
        this.pdfUrl = this.sanitizer.bypassSecurityTrustResourceUrl(objectUrl);
        this.loading = false;
        document.title = 'Print Account Closing Report';
        this.cdr.detectChanges();
      } else {
        this.error = `Failed to load account closing report PDF (${xhr.status})`;
        this.loading = false;
        this.cdr.detectChanges();
      }
    };

    xhr.onerror = () => {
      this.error = 'Network error loading account closing report PDF';
      this.loading = false;
      this.cdr.detectChanges();
    };

    xhr.send();
  }
}
