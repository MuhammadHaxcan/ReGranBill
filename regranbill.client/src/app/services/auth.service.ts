import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { AppUser, LoginRequest, LoginResponse, UserRole } from '../models/auth.model';

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
          role: res.role as UserRole,
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
    return this.currentUser?.role === UserRole.Admin;
  }

  syncCurrentUser(update: Partial<Pick<AppUser, 'username' | 'fullName' | 'role'>>): void {
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
