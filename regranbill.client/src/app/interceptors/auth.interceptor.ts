import { Injectable } from '@angular/core';
import {
  HttpErrorResponse,
  HttpInterceptor,
  HttpRequest,
  HttpHandler,
  HttpEvent,
} from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

@Injectable()
export class AuthInterceptor implements HttpInterceptor {
  constructor(private authService: AuthService, private router: Router) {}

  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    const user = this.authService.currentUser;
    if (user && req.url.startsWith('/api') && !req.url.includes('/api/auth/login')) {
      req = req.clone({
        setHeaders: { Authorization: `Bearer ${user.token}` },
      });
    }

    return next.handle(req).pipe(
      catchError((error: HttpErrorResponse) => {
        const isAuthCall = req.url.includes('/api/auth/login');
        if (!isAuthCall && (error.status === 401 || error.status === 403)) {
          this.authService.logout();
          if (this.router.url !== '/login') {
            this.router.navigate(['/login']);
          }
        }
        return throwError(() => error);
      })
    );
  }
}
