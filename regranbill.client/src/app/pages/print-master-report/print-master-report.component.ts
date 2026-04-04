import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-print-master-report',
  templateUrl: './print-master-report.component.html',
  styleUrl: './print-master-report.component.css',
  standalone: false
})
export class PrintMasterReportComponent implements OnInit {
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
    const categoryId = this.route.snapshot.queryParamMap.get('categoryId');
    const accountId = this.route.snapshot.queryParamMap.get('accountId');
    const columns = this.route.snapshot.queryParamMap.get('columns');

    if (from) query.set('from', from);
    if (to) query.set('to', to);
    if (categoryId) query.set('categoryId', categoryId);
    if (accountId) query.set('accountId', accountId);
    if (columns) query.set('columns', columns);

    const url = query.toString()
      ? `/api/master-report/pdf?${query.toString()}`
      : '/api/master-report/pdf';

    const xhr = new XMLHttpRequest();
    xhr.open('GET', url, true);
    xhr.setRequestHeader('Authorization', `Bearer ${token}`);
    xhr.responseType = 'blob';

    xhr.onload = () => {
      if (xhr.status === 200) {
        const objectUrl = URL.createObjectURL(xhr.response);
        this.pdfUrl = this.sanitizer.bypassSecurityTrustResourceUrl(objectUrl);
        this.loading = false;
        document.title = 'Print Master Report';
        this.cdr.detectChanges();
      } else {
        this.error = `Failed to load master report PDF (${xhr.status})`;
        this.loading = false;
        this.cdr.detectChanges();
      }
    };

    xhr.onerror = () => {
      this.error = 'Network error loading master report PDF';
      this.loading = false;
      this.cdr.detectChanges();
    };

    xhr.send();
  }
}
