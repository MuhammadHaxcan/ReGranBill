import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-print-product-stock-report',
  templateUrl: './print-product-stock-report.component.html',
  styleUrl: './print-product-stock-report.component.css',
  standalone: false
})
export class PrintProductStockReportComponent implements OnInit {
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
    const token = this.authService.currentUser?.token;
    if (!token) {
      this.authService.logout();
      this.router.navigate(['/login']);
      return;
    }

    const query = new URLSearchParams();
    const from = this.route.snapshot.queryParamMap.get('from');
    const to = this.route.snapshot.queryParamMap.get('to');
    const categoryId = this.route.snapshot.queryParamMap.get('categoryId');
    const productId = this.route.snapshot.queryParamMap.get('productId');
    const selectedMovementProductId = this.route.snapshot.queryParamMap.get('selectedMovementProductId');

    if (from) query.set('from', from);
    if (to) query.set('to', to);
    if (categoryId) query.set('categoryId', categoryId);
    if (productId) query.set('productId', productId);
    if (selectedMovementProductId) query.set('selectedMovementProductId', selectedMovementProductId);
    query.set('includeDetails', 'true');

    const url = `/api/product-stock-report/pdf?${query.toString()}`;

    const xhr = new XMLHttpRequest();
    xhr.open('GET', url, true);
    xhr.setRequestHeader('Authorization', `Bearer ${token}`);
    xhr.responseType = 'blob';

    xhr.onload = () => {
      if (xhr.status === 200) {
        const objectUrl = URL.createObjectURL(xhr.response);
        this.pdfUrl = this.sanitizer.bypassSecurityTrustResourceUrl(objectUrl);
        document.title = 'Print Product Stock Report';
        this.cdr.detectChanges();
      } else if (xhr.status === 401 || xhr.status === 403) {
        this.authService.logout();
        this.router.navigate(['/login']);
      } else {
        this.error = `Failed to load product stock report PDF (${xhr.status})`;
        this.loading = false;
        this.cdr.detectChanges();
      }
    };

    xhr.onerror = () => {
      this.error = 'Network error loading product stock report PDF';
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
