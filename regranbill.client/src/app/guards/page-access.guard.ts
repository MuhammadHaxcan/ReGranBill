import { Injectable } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivate, Router, UrlTree } from '@angular/router';
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

  canActivate(route: ActivatedRouteSnapshot): boolean | UrlTree {
    if (!this.authService.isLoggedIn) {
      return this.router.parseUrl('/login');
    }

    const pageKey = route.data?.['pageKey'] as string | undefined;
    if (!pageKey) {
      // If a protected route forgets its pageKey we fail closed.
      console.warn('PageAccessGuard: route missing data.pageKey', route.url);
      return this.router.parseUrl('/login');
    }

    if (this.authService.hasPage(pageKey)) {
      return true;
    }

    this.toast.info("You don't have access to that page. Ask an admin to update your role.");
    const fallback = this.authService.firstAccessibleRoute();
    return this.router.parseUrl(fallback ?? '/login');
  }
}
