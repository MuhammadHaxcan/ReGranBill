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
    return this.currentUserSubject.value;
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
    return '/home';
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
      if (!parsed?.token) {
        localStorage.removeItem('currentUser');
        return null;
      }
      return parsed;
    } catch {
      localStorage.removeItem('currentUser');
      return null;
    }
  }
}
