import { Component, OnDestroy, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { AuthService } from './services/auth.service';
import { Toast, ToastService } from './services/toast.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.html',
  standalone: false,
  styleUrl: './app.css'
})
export class App implements OnInit, OnDestroy {
  toastVisible = false;
  toastMessage = '';
  toastType: 'success' | 'error' | 'info' = 'success';
  private toastSub!: Subscription;
  private toastTimer: any;

  constructor(
    public authService: AuthService,
    private router: Router,
    private toastService: ToastService
  ) {}

  ngOnInit(): void {
    this.toastSub = this.toastService.toast$.subscribe((toast: Toast) => {
      this.showToast(toast);
    });
  }

  ngOnDestroy(): void {
    this.toastSub?.unsubscribe();
    clearTimeout(this.toastTimer);
  }

  private showToast(toast: Toast): void {
    clearTimeout(this.toastTimer);
    this.toastMessage = toast.message;
    this.toastType = toast.type;
    this.toastVisible = true;
    this.toastTimer = setTimeout(() => {
      this.toastVisible = false;
    }, 4000);
  }

  dismissToast(): void {
    clearTimeout(this.toastTimer);
    this.toastVisible = false;
  }

  get isLoginPage(): boolean {
    return this.router.url === '/login';
  }

  get isPrintPage(): boolean {
    return this.router.url.startsWith('/print-dc') || this.router.url.startsWith('/print-pv');
  }

  get isAdmin(): boolean {
    return this.authService.isAdmin();
  }

  get userInitial(): string {
    const name = this.authService.currentUser?.fullName || '';
    return name.charAt(0).toUpperCase();
  }

  get userName(): string {
    return this.authService.currentUser?.fullName || '';
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
