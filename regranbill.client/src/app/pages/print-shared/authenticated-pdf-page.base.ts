import { ChangeDetectorRef, Directive, OnDestroy, inject } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Directive()
export abstract class AuthenticatedPdfPageBase implements OnDestroy {
  protected readonly route = inject(ActivatedRoute);
  protected readonly router = inject(Router);
  protected readonly sanitizer = inject(DomSanitizer);
  protected readonly authService = inject(AuthService);
  protected readonly cdr = inject(ChangeDetectorRef);

  pdfUrl: SafeResourceUrl | null = null;
  downloadUrl: string | null = null;
  fileName = '';
  loading = true;
  error = '';

  private objectUrl: string | null = null;

  protected requireRouteParam(name: string, errorMessage: string): string | null {
    const value = this.route.snapshot.paramMap.get(name);
    if (value) {
      return value;
    }

    this.setError(errorMessage);
    return null;
  }

  protected buildUrl(basePath: string, params: Record<string, string | null | undefined>): string {
    const query = new URLSearchParams();

    Object.entries(params).forEach(([key, value]) => {
      if (value !== null && value !== undefined && value !== '') {
        query.set(key, value);
      }
    });

    const queryString = query.toString();
    return queryString ? `${basePath}?${queryString}` : basePath;
  }

  protected loadPdf(url: string, title: string): void {
    const token = this.authService.currentUser?.token;
    if (!token) {
      this.authService.logout();
      this.router.navigate(['/login']);
      return;
    }

    const xhr = new XMLHttpRequest();
    xhr.open('GET', url, true);
    xhr.setRequestHeader('Authorization', `Bearer ${token}`);
    xhr.responseType = 'blob';

    xhr.onload = () => {
      if (xhr.status === 200) {
        this.setPdfResponse(
          xhr.response,
          title,
          this.parseFileName(xhr.getResponseHeader('Content-Disposition')) ?? this.buildFallbackFileName(title)
        );
        return;
      }

      if (xhr.status === 401) {
        // Token invalid / expired — sign out and send to login.
        this.authService.logout();
        this.router.navigate(['/login']);
        return;
      }

      if (xhr.status === 403) {
        this.setError("You don't have permission to view this document. Ask an admin to grant access.");
        return;
      }

      if (xhr.status === 404) {
        this.setError('Document not found.');
        return;
      }

      this.setError(`Unable to load the document (HTTP ${xhr.status}). Please try again.`);
    };

    xhr.onerror = () => {
      this.setError('Network error while loading the document. Check your connection and try again.');
    };

    xhr.send();
  }

  onIframeLoad(): void {
    if (!this.loading) {
      return;
    }

    this.loading = false;
    this.cdr.detectChanges();
  }

  ngOnDestroy(): void {
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
    }
  }

  protected setError(message: string): void {
    this.error = message;
    this.loading = false;
    this.cdr.detectChanges();
  }

  private setPdfResponse(blob: Blob, title: string, fileName: string): void {
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
    }

    const pdfFile = new File([blob], fileName, { type: blob.type || 'application/pdf' });
    this.objectUrl = URL.createObjectURL(pdfFile);
    this.pdfUrl = this.sanitizer.bypassSecurityTrustResourceUrl(this.objectUrl);
    this.downloadUrl = this.objectUrl;
    this.fileName = pdfFile.name;
    document.title = pdfFile.name || title;
    this.cdr.detectChanges();
  }

  private parseFileName(contentDisposition: string | null): string | null {
    if (!contentDisposition) return null;

    const utf8Match = contentDisposition.match(/filename\*\s*=\s*UTF-8''([^;]+)/i);
    if (utf8Match?.[1]) {
      try {
        return decodeURIComponent(utf8Match[1].trim()).replace(/^["']|["']$/g, '');
      } catch {
        return utf8Match[1].trim().replace(/^["']|["']$/g, '');
      }
    }

    const fileNameMatch = contentDisposition.match(/filename\s*=\s*("?)([^";]+)\1/i);
    return fileNameMatch?.[2]?.trim() || null;
  }

  private buildFallbackFileName(title: string): string {
    const normalized = title
      .replace(/[^A-Za-z0-9._-]+/g, '_')
      .replace(/_+/g, '_')
      .replace(/^_+|_+$/g, '');

    return `${normalized || 'document'}.pdf`;
  }
}
