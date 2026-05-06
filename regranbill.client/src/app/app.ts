import { Component, ChangeDetectorRef, HostListener, OnDestroy, OnInit } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter, Subscription } from 'rxjs';
import { AuthService } from './services/auth.service';
import { Toast, ToastService } from './services/toast.service';
import { ConfirmModal, ConfirmModalService } from './services/confirm-modal.service';
import { PAGE_GROUPS, PAGES, PageDefinition, PageGroup } from './config/page-catalog';

interface SidebarGroup {
  group: PageGroup;
  label: string;
  pages: PageDefinition[];
}

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
  visibleGroups: SidebarGroup[] = [];
  /** Bumps each time a new toast replaces the visible one. Used as an *ngFor key
   *  to retrigger the entry animation so a swap feels like a new pop, not a relabel. */
  toastSeq = 0;
  private toastSub!: Subscription;
  private routeSub!: Subscription;
  private userSub!: Subscription;

  // Each group is open by default; user can collapse via the chevron.
  private collapsedGroups = new Set<PageGroup>();

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
    this.updateVisibleGroups();
    this.routeSub = this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe(() => this.updatePageTitle());
    this.userSub = this.authService.currentUser$.subscribe(() => this.updateVisibleGroups());

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
      
    });
  }

  ngOnDestroy(): void {
    this.toastSub?.unsubscribe();
    this.routeSub?.unsubscribe();
    this.modalSub?.unsubscribe();
    this.userSub?.unsubscribe();
  }

  private showToast(toast: Toast): void {
    // Sticky toasts: stay visible until the user closes them or a newer toast arrives.
    // The seq bump retriggers the CSS swap animation so a replacement feels like a new pop.
    this.toastMessage = toast.message;
    this.toastType = toast.type;
    this.toastVisible = true;
    this.toastSeq++;
  }

  dismissToast(): void {
    this.toastVisible = false;
  }

  /** trackBy that uses the seq as identity, so each new toast remounts the element
   *  and the CSS swap animation replays. */
  trackToastSeq = (_: number, seq: number): number => seq;

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

  get userInitial(): string {
    const name = this.authService.currentUser?.fullName || '';
    return name.charAt(0).toUpperCase();
  }

  get userName(): string {
    return this.authService.currentUser?.fullName || '';
  }

  get userRoleLabel(): string {
    return this.authService.currentUser?.roleName || '';
  }

  trackSidebarGroup = (_: number, group: SidebarGroup): string => group.group;

  trackSidebarPage = (_: number, page: PageDefinition): string => page.key;

  private updateVisibleGroups(): void {
    this.visibleGroups = PAGE_GROUPS
      .map(g => ({
        group: g.group,
        label: g.label,
        pages: PAGES.filter(p => p.group === g.group && !p.hidden && this.authService.hasPage(p.key))
      }))
      .filter(g => g.pages.length > 0);
  }

  isGroupOpen(group: PageGroup): boolean {
    return !this.collapsedGroups.has(group);
  }

  toggleGroup(group: PageGroup): void {
    if (this.collapsedGroups.has(group)) {
      this.collapsedGroups.delete(group);
    } else {
      this.collapsedGroups.add(group);
    }
  }

  navigateTo(route: string): void {
    if (!route) return;
    void this.router.navigateByUrl(route);
  }

  isPageActive(route: string): boolean {
    if (!route) return false;
    const currentPath = this.router.url.split('?')[0].toLowerCase();
    const targetPath = route.toLowerCase();
    return currentPath === targetPath || currentPath.startsWith(`${targetPath}/`);
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }

  private updatePageTitle(): void {
    const path = this.router.url.split('?')[0].toLowerCase();
    const projectName = 'ReGranBooks';

    const routeTitles: Array<{ startsWith: string; title: string }> = [
      { startsWith: '/login', title: 'Login' },
      { startsWith: '/delivery-challan', title: 'Delivery Challan' },
      { startsWith: '/purchase-voucher', title: 'Purchase Voucher' },
      { startsWith: '/sale-return', title: 'Sale Return' },
      { startsWith: '/purchase-return', title: 'Purchase Return' },
      { startsWith: '/journal-voucher', title: 'Journal Voucher' },
      { startsWith: '/receipt-voucher', title: 'Receipt Voucher' },
      { startsWith: '/payment-voucher', title: 'Payment Voucher' },
      { startsWith: '/voucher-editor', title: 'Voucher Editor' },
      { startsWith: '/pending', title: 'Pending Review' },
      { startsWith: '/rated-vouchers', title: 'Rated Vouchers' },
      { startsWith: '/customer-ledger', title: 'Customer / Vendor Ledger' },
      { startsWith: '/soa', title: 'Statement of Account' },
      { startsWith: '/master-report', title: 'Master Report' },
      { startsWith: '/account-closing-report', title: 'Account Closing Report' },
      { startsWith: '/sale-purchase-report', title: 'Sale Purchase Register' },
      { startsWith: '/product-stock-report', title: 'Product Stock Report' },
      { startsWith: '/metadata', title: 'Metadata' },
      { startsWith: '/company-settings', title: 'Company Settings' },
      { startsWith: '/users', title: 'User Management' },
      { startsWith: '/roles', title: 'Roles' },
      { startsWith: '/add-rate', title: 'Add Rate' },
      { startsWith: '/add-purchase-rate', title: 'Add Purchase Rate' },
      { startsWith: '/add-sale-return-rate', title: 'Add Sale Return Rate' },
      { startsWith: '/add-purchase-return-rate', title: 'Add Purchase Return Rate' }
    ];

    const matched = routeTitles.find(r => path.startsWith(r.startsWith));
    document.title = matched ? `${projectName} - ${matched.title}` : projectName;
  }
}
