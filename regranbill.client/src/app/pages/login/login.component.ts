import { Component } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ChangeDetectorRef } from '@angular/core';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrl: './login.component.css',
  standalone: false,
})
export class LoginComponent {
  username = '';
  password = '';
  error = '';
  loading = false;

  constructor(
    private authService: AuthService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {
    if (this.authService.isLoggedIn) {
      this.router.navigate([this.authService.firstAccessibleRoute() ?? '/login']);
    }
  }

  login(): void {
    const username = this.username.trim();
    const password = this.password;

    if (!username || !password) {
      this.error = 'Please enter username and password';
      return;
    }

    this.loading = true;
    this.error = '';

    this.authService.login({ username, password }).subscribe({
      next: () => {
        this.loading = false;
        this.cdr.detectChanges();
        this.router.navigate([this.authService.firstAccessibleRoute() ?? '/login']);
      },
      error: (err: HttpErrorResponse) => {
        this.handleLoginError(err);
        this.loading = false;
        this.cdr.detectChanges();
      },
    });
  }

  private handleLoginError(err: HttpErrorResponse): void {
    this.error = this.resolveErrorMessage(err);
    this.cdr.detectChanges();

    // Some 401 responses arrive as Blob/text. Parse those too and update message.
    if (err.error instanceof Blob) {
      err.error.text().then((text) => {
        if (!text) return;
        try {
          const parsed = JSON.parse(text) as { message?: string };
          this.error = parsed?.message || this.error;
        } catch {
          this.error = text;
        }
        this.cdr.detectChanges();
      });
    }
  }

  private resolveErrorMessage(err: HttpErrorResponse): string {
    if (err.error && typeof err.error === 'object' && 'message' in err.error) {
      return String(err.error.message || 'Login failed');
    }

    if (typeof err.error === 'string') {
      try {
        const parsed = JSON.parse(err.error) as { message?: string };
        if (parsed?.message) {
          return parsed.message;
        }
      } catch {
        // Ignore parse errors and fall through.
      }
    }

    return 'Login failed';
  }
}
