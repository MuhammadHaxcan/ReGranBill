import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from './services/auth.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.html',
  standalone: false,
  styleUrl: './app.css'
})
export class App {
  constructor(public authService: AuthService, private router: Router) {}

  get isLoginPage(): boolean {
    return this.router.url === '/login';
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
