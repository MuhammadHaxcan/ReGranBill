import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { AppUser, LoginRequest, LoginResponse } from '../models/auth.model';
import { PAGES } from '../config/page-catalog';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private currentUserSubject = new BehaviorSubject<AppUser | null>(this.loadUser());
  currentUser$ = this.currentUserSubject.asObservable();

  constructor(private http: HttpClient) {}

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>('/api/auth/login', request).pipe(
      tap((res) => {
        const user: AppUser = {
          username: res.username,
          fullName: res.fullName,
          roleId: res.roleId,
          roleName: res.roleName,
          isAdmin: res.isAdmin,
          pages: res.pages ?? [],
          token: res.token,
        };
        localStorage.setItem('currentUser', JSON.stringify(user));
        this.currentUserSubject.next(user);
      })
    );
  }

  logout(): void {
    localStorage.removeItem('currentUser');
    this.currentUserSubject.next(null);
  }

  get currentUser(): AppUser | null {
    const user = this.currentUserSubject.value;
    if (!user) return null;

    if (this.isTokenExpired(user.token)) {
      this.logout();
      return null;
    }

    return user;
  }

  get isLoggedIn(): boolean {
    return this.currentUser !== null;
  }

  isAdmin(): boolean {
    return this.currentUser?.isAdmin === true;
  }

  hasPage(key: string): boolean {
    const user = this.currentUser;
    if (!user) return false;
    if (user.isAdmin) return true;
    return user.pages.includes(key);
  }

  /** First sidebar route the user can navigate to. Used for default redirects. */
  firstAccessibleRoute(): string | null {
    const user = this.currentUser;
    if (!user) return null;
    for (const page of PAGES) {
      if (user.isAdmin || user.pages.includes(page.key)) {
        return page.route;
      }
    }
    return null;
  }

  syncCurrentUser(update: Partial<Pick<AppUser, 'username' | 'fullName' | 'roleId' | 'roleName' | 'isAdmin' | 'pages'>>): void {
    const currentUser = this.currentUser;
    if (!currentUser) return;

    const nextUser: AppUser = {
      ...currentUser,
      ...update
    };

    localStorage.setItem('currentUser', JSON.stringify(nextUser));
    this.currentUserSubject.next(nextUser);
  }

  private loadUser(): AppUser | null {
    const stored = localStorage.getItem('currentUser');
    if (!stored) return null;
    try {
      const parsed = JSON.parse(stored) as AppUser;
      if (!parsed?.token || this.isTokenExpired(parsed.token)) {
        localStorage.removeItem('currentUser');
        return null;
      }
      return parsed;
    } catch {
      localStorage.removeItem('currentUser');
      return null;
    }
  }

  private isTokenExpired(token: string): boolean {
    try {
      const parts = token.split('.');
      if (parts.length !== 3) return true;

      const payloadRaw = parts[1].replace(/-/g, '+').replace(/_/g, '/');
      const paddedPayload = payloadRaw + '='.repeat((4 - (payloadRaw.length % 4)) % 4);
      const payload = JSON.parse(atob(paddedPayload)) as { exp?: number };

      if (typeof payload.exp !== 'number') return true;
      return payload.exp * 1000 <= Date.now();
    } catch {
      return true;
    }
  }
}
