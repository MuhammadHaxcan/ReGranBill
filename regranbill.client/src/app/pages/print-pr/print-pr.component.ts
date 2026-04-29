import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-print-pr',
  templateUrl: './print-pr.component.html',
  styleUrl: './print-pr.component.css',
  standalone: false
})
export class PrintPrComponent implements OnInit {
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
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error = 'No purchase return ID provided';
      this.loading = false;
      return;
    }

    const token = this.authService.currentUser?.token;
    if (!token) {
      this.authService.logout();
      this.router.navigate(['/login']);
      return;
    }

    const xhr = new XMLHttpRequest();
    xhr.open('GET', `/api/purchase-returns/${id}/pdf`, true);
    xhr.setRequestHeader('Authorization', `Bearer ${token}`);
    xhr.responseType = 'blob';

    xhr.onload = () => {
      if (xhr.status === 200) {
        const blob = xhr.response;
        const url = URL.createObjectURL(blob);
        this.pdfUrl = this.sanitizer.bypassSecurityTrustResourceUrl(url);
        document.title = `Print PR - ${id}`;
        this.cdr.detectChanges();
      } else if (xhr.status === 401 || xhr.status === 403) {
        this.authService.logout();
        this.router.navigate(['/login']);
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

  onIframeLoad(): void {
    this.loading = false;
    this.cdr.detectChanges();
  }
}