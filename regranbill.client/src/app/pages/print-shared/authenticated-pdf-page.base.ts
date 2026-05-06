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
        this.setPdfResponse(xhr.response, title);
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
    
  }

  ngOnDestroy(): void {
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
    }
  }

  protected setError(message: string): void {
    this.error = message;
    this.loading = false;
    
  }

  private setPdfResponse(blob: Blob, title: string): void {
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
    }

    this.objectUrl = URL.createObjectURL(blob);
    this.pdfUrl = this.sanitizer.bypassSecurityTrustResourceUrl(this.objectUrl);
    document.title = title;
    
  }
}
