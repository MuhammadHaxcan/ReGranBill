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
import { ToastService } from '../services/toast.service';

@Injectable()
export class AuthInterceptor implements HttpInterceptor {
  constructor(
    private authService: AuthService,
    private router: Router,
    private toast: ToastService
  ) {}

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
        if (!isAuthCall) {
          if (error.status === 401) {
            this.toast.error('A request was rejected as unauthorized. Refresh the page or sign in again if this keeps happening.');
          } else if (error.status === 403) {
            this.toast.error("You don't have permission to do that. Ask an admin to grant access.");
          }
        }
        return throwError(() => error);
      })
    );
  }
}
