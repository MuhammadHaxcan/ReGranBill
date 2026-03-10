import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { HttpClient } from '@angular/common/http';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-print-pv',
  templateUrl: './print-pv.component.html',
  styleUrl: './print-pv.component.css',
  standalone: false
})
export class PrintPvComponent implements OnInit {
  pdfUrl: SafeResourceUrl | null = null;
  loading = true;
  error = '';

  constructor(
    private route: ActivatedRoute,
    private http: HttpClient,
    private sanitizer: DomSanitizer,
    private authService: AuthService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error = 'No purchase voucher ID provided';
      this.loading = false;
      return;
    }

    const token = this.authService.currentUser?.token;
    if (!token) {
      this.error = 'Not authenticated';
      this.loading = false;
      return;
    }

    const xhr = new XMLHttpRequest();
    xhr.open('GET', `/api/purchase-vouchers/${id}/pdf`, true);
    xhr.setRequestHeader('Authorization', `Bearer ${token}`);
    xhr.responseType = 'blob';

    xhr.onload = () => {
      if (xhr.status === 200) {
        const blob = xhr.response;
        const url = URL.createObjectURL(blob);
        this.pdfUrl = this.sanitizer.bypassSecurityTrustResourceUrl(url);
        this.loading = false;
        document.title = `Print PV - ${id}`;
        this.cdr.detectChanges();
      } else {
        this.error = `Failed to load PDF (${xhr.status})`;
        this.loading = false;
        this.cdr.detectChanges();
      }
    };

    xhr.onerror = () => {
      this.error = 'Network error loading PDF';
      this.loading = false;
      this.cdr.detectChanges();
    };

    xhr.send();
  }
}
