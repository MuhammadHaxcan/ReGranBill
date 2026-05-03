import { ChangeDetectorRef, Component, HostListener, OnInit } from '@angular/core';
import { ConfirmModalService } from '../../services/confirm-modal.service';
import { ToastService } from '../../services/toast.service';
import { RoleService } from '../../services/role.service';
import { Role } from '../../models/role.model';
import { PAGE_GROUPS, PAGES, PageDefinition, PageGroup } from '../../config/page-catalog';
import { getApiErrorMessage } from '../../utils/api-error';

interface PageGroupView {
  group: PageGroup;
  label: string;
  pages: PageDefinition[];
}

@Component({
  selector: 'app-role-management',
  templateUrl: './role-management.component.html',
  styleUrl: './role-management.component.css',
  standalone: false
})
export class RoleManagementComponent implements OnInit {
  roles: Role[] = [];
  searchText = '';
  loading = false;
  showModal = false;
  editingRoleId: number | null = null;
  editingIsSystem = false;
  editingIsAdmin = false;
  formError = '';

  name = '';
  selectedPages = new Set<string>();

  readonly pageGroups: PageGroupView[] = PAGE_GROUPS.map(g => ({
    group: g.group,
    label: g.label,
    pages: PAGES.filter(p => p.group === g.group)
  }));

  constructor(
    private roleService: RoleService,
    private toast: ToastService,
    private confirmModal: ConfirmModalService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadRoles();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.showModal) {
      this.closeModal();
    }
  }

  get filteredRoles(): Role[] {
    if (!this.searchText.trim()) return this.roles;
    const q = this.searchText.trim().toLowerCase();
    return this.roles.filter(r => r.name.toLowerCase().includes(q));
  }

  loadRoles(): void {
    this.loading = true;
    this.roleService.getAll().subscribe({
      next: roles => {
        this.roles = roles;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.loading = false;
        this.toast.error('Unable to load roles.');
        this.cdr.detectChanges();
      }
    });
  }

  openAddModal(): void {
    this.editingRoleId = null;
    this.editingIsSystem = false;
    this.editingIsAdmin = false;
    this.resetForm();
    this.showModal = true;
  }

  openEditModal(role: Role): void {
    this.editingRoleId = role.id;
    this.editingIsSystem = role.isSystem;
    this.editingIsAdmin = role.isAdmin;
    this.name = role.name;
    this.selectedPages = new Set(role.pages);
    this.formError = '';
    this.showModal = true;
  }

  closeModal(): void {
    this.showModal = false;
    this.resetForm();
  }

  resetForm(): void {
    this.name = '';
    this.selectedPages = new Set();
    this.formError = '';
  }

  togglePage(key: string): void {
    if (this.editingIsSystem) return;
    if (this.selectedPages.has(key)) {
      this.selectedPages.delete(key);
    } else {
      this.selectedPages.add(key);
    }
  }

  isPageSelected(key: string): boolean {
    if (this.editingIsAdmin) return true;
    return this.selectedPages.has(key);
  }

  isGroupAllSelected(group: PageGroupView): boolean {
    if (this.editingIsAdmin) return true;
    return group.pages.every(p => this.selectedPages.has(p.key));
  }

  isGroupSomeSelected(group: PageGroupView): boolean {
    if (this.editingIsAdmin) return true;
    return group.pages.some(p => this.selectedPages.has(p.key)) && !this.isGroupAllSelected(group);
  }

  toggleGroup(group: PageGroupView): void {
    if (this.editingIsSystem) return;
    if (this.isGroupAllSelected(group)) {
      group.pages.forEach(p => this.selectedPages.delete(p.key));
    } else {
      group.pages.forEach(p => this.selectedPages.add(p.key));
    }
  }

  saveRole(): void {
    if (this.editingIsSystem) {
      this.toast.info('System roles cannot be modified.');
      return;
    }

    const name = this.name.trim();
    if (!name) {
      this.formError = 'Role name is required.';
      return;
    }

    if (this.selectedPages.size === 0) {
      this.formError = 'Select at least one page.';
      return;
    }

    this.formError = '';
    const payload = { name, pages: Array.from(this.selectedPages) };

    const obs = this.editingRoleId == null
      ? this.roleService.create(payload)
      : this.roleService.update(this.editingRoleId, payload);

    obs.subscribe({
      next: () => {
        this.toast.success(this.editingRoleId == null ? 'Role created.' : 'Role updated.');
        this.closeModal();
        this.loadRoles();
      },
      error: err => {
        this.formError = getApiErrorMessage(err, 'Unable to save role.');
        this.cdr.detectChanges();
      }
    });
  }

  async deleteRole(role: Role): Promise<void> {
    if (role.isSystem) {
      this.toast.info('System roles cannot be deleted.');
      return;
    }

    const confirmed = await this.confirmModal.confirm({
      title: 'Delete Role',
      message: `Delete role "${role.name}"? This cannot be undone.`,
      confirmText: 'Delete',
      cancelText: 'Cancel'
    });

    if (!confirmed) return;

    this.roleService.delete(role.id).subscribe({
      next: () => {
        this.toast.success('Role deleted.');
        this.loadRoles();
      },
      error: err => {
        this.toast.error(getApiErrorMessage(err, 'Unable to delete role.'));
      }
    });
  }
}
