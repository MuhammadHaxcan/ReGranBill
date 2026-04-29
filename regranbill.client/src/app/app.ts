import { Component, ChangeDetectorRef, HostListener, OnDestroy, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { AuthService } from './services/auth.service';
import { Toast, ToastService } from './services/toast.service';
import { ConfirmModal, ConfirmModalService } from './services/confirm-modal.service';

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

  // Sidebar dropdown groups
  saleGroupOpen = true;
  purchaseGroupOpen = true;
  returnGroupOpen = true;
  pendingGroupOpen = true;

  // Confirm modal
  modalVisible = false;
  modalData: ConfirmModal | null = null;
  private modalSub!: Subscription;

  constructor(
    public authService: AuthService,
    private router: Router,
    private toastService: ToastService,
    private confirmModalService: ConfirmModalService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.toastSub = this.toastService.toast$.subscribe((toast: Toast) => {
      this.showToast(toast);
    });
    this.modalSub = this.confirmModalService.modal$.subscribe(modal => {
      if (modal) {
        this.modalData = modal;
        this.modalVisible = true;
      } else {
        this.modalVisible = false;
        this.modalData = null;
      }
      this.cdr.detectChanges();
    });
  }

  ngOnDestroy(): void {
    this.toastSub?.unsubscribe();
    this.modalSub?.unsubscribe();
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

  // Modal methods
  onModalConfirm(): void {
    if (this.modalData?.onConfirm) {
      this.modalData.onConfirm();
    }
    this.confirmModalService.close();
  }

  onModalClose(): void {
    this.confirmModalService.close();
  }

  @HostListener('document:keydown.escape')
  onEscapeModal(): void {
    if (this.modalVisible) {
      this.onModalClose();
    }
  }

  get isLoginPage(): boolean {
    return this.router.url === '/login';
  }

  get isPrintPage(): boolean {
    return this.router.url.startsWith('/print-dc') ||
      this.router.url.startsWith('/print-pv') ||
      this.router.url.startsWith('/print-sr') ||
      this.router.url.startsWith('/print-pr') ||
      this.router.url.startsWith('/print-soa') ||
      this.router.url.startsWith('/print-master-report') ||
      this.router.url.startsWith('/print-account-closing-report') ||
      this.router.url.startsWith('/print-product-stock-report') ||
      this.router.url.startsWith('/print-customer-ledger');
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

  toggleGroup(group: 'sale' | 'purchase' | 'return' | 'pending'): void {
    if (group === 'sale') this.saleGroupOpen = !this.saleGroupOpen;
    if (group === 'purchase') this.purchaseGroupOpen = !this.purchaseGroupOpen;
    if (group === 'return') this.returnGroupOpen = !this.returnGroupOpen;
    if (group === 'pending') this.pendingGroupOpen = !this.pendingGroupOpen;
  }
}
