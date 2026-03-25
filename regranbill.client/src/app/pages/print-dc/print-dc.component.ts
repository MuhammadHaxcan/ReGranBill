import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-print-dc',
  templateUrl: './print-dc.component.html',
  styleUrl: './print-dc.component.css',
  standalone: false
})
export class PrintDcComponent implements OnInit {
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
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error = 'No challan ID provided';
      this.loading = false;
      return;
    }

    const token = this.authService.currentUser?.token;
    if (!token) {
      this.error = 'Not authenticated';
      this.loading = false;
      return;
    }

    // Fetch PDF with explicit auth header
    const xhr = new XMLHttpRequest();
    xhr.open('GET', `/api/delivery-challans/${id}/pdf`, true);
    xhr.setRequestHeader('Authorization', `Bearer ${token}`);
    xhr.responseType = 'blob';

    xhr.onload = () => {
      if (xhr.status === 200) {
        const blob = xhr.response;
        const url = URL.createObjectURL(blob);
        this.pdfUrl = this.sanitizer.bypassSecurityTrustResourceUrl(url);
        this.loading = false;
        // Set tab title
        document.title = `Print DC - ${id}`;
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
