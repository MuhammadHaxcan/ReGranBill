import { Injectable } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivate, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { ToastService } from '../services/toast.service';

/**
 * Class-based guard so it can be referenced from the existing NgModule routing
 * config alongside AuthGuard. The route declares its `pageKey` via `data` and
 * this guard checks `authService.hasPage(...)`. If the user lacks access, they
 * are bounced to the first route they CAN reach (or to /login if none) with a
 * friendly toast explaining why.
 */
@Injectable({
  providedIn: 'root',
})
export class PageAccessGuard implements CanActivate {
  constructor(
    private authService: AuthService,
    private router: Router,
    private toast: ToastService
  ) {}

  canActivate(route: ActivatedRouteSnapshot): boolean {
    if (!this.authService.isLoggedIn) {
      this.router.navigate(['/login']);
      return false;
    }

    const pageKey = route.data?.['pageKey'] as string | undefined;
    if (!pageKey) {
      // If a protected route forgets its pageKey we fail closed.
      console.warn('PageAccessGuard: route missing data.pageKey', route.url);
      this.router.navigate(['/login']);
      return false;
    }

    if (this.authService.hasPage(pageKey)) {
      return true;
    }

    this.toast.info("You don't have access to that page. Ask an admin to update your role.");
    const fallback = this.authService.firstAccessibleRoute();
    this.router.navigate([fallback ?? '/login']);
    return false;
  }
}
