import { ChangeDetectorRef, Component, HostListener, OnInit } from '@angular/core';
import { AuthService } from '../../services/auth.service';
import { ConfirmModalService } from '../../services/confirm-modal.service';
import { ToastService } from '../../services/toast.service';
import { UserManagementService } from '../../services/user-management.service';
import { RoleService } from '../../services/role.service';
import { ManagedUser } from '../../models/user-management.model';
import { Role } from '../../models/role.model';
import { formatDateDisplay } from '../../utils/date-utils';
import { getApiErrorMessage } from '../../utils/api-error';

@Component({
  selector: 'app-user-management',
  templateUrl: './user-management.component.html',
  styleUrl: './user-management.component.css',
  standalone: false
})
export class UserManagementComponent implements OnInit {
  users: ManagedUser[] = [];
  roles: Role[] = [];
  searchText = '';
  loading = false;
  showModal = false;
  editingUserId: number | null = null;
  formError = '';

  username = '';
  fullName = '';
  password = '';
  roleId: number | null = null;
  isActive = true;

  constructor(
    private userManagementService: UserManagementService,
    private roleService: RoleService,
    private authService: AuthService,
    private toast: ToastService,
    private confirmModal: ConfirmModalService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadRoles();
    this.loadUsers();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.showModal) {
      this.closeModal();
    }
  }

  get filteredUsers(): ManagedUser[] {
    if (!this.searchText.trim()) return this.users;
    const term = this.searchText.trim().toLowerCase();
    return this.users.filter(user =>
      user.username.toLowerCase().includes(term) ||
      user.fullName.toLowerCase().includes(term) ||
      user.roleName.toLowerCase().includes(term)
    );
  }

  loadRoles(): void {
    this.roleService.getAll().subscribe({
      next: roles => {
        this.roles = roles;
        if (this.roleId === null && roles.length > 0) {
          this.roleId = roles[0].id;
        }
        this.cdr.detectChanges();
      },
      error: () => this.toast.error('Unable to load roles.')
    });
  }

  loadUsers(): void {
    this.loading = true;
    this.userManagementService.getAll().subscribe({
      next: users => {
        this.users = users;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.loading = false;
        this.toast.error('Unable to load users.');
        this.cdr.detectChanges();
      }
    });
  }

  openAddModal(): void {
    this.editingUserId = null;
    this.resetForm();
    this.showModal = true;
  }

  openEditModal(user: ManagedUser): void {
    this.editingUserId = user.id;
    this.username = user.username;
    this.fullName = user.fullName;
    this.password = '';
    this.roleId = user.roleId;
    this.isActive = user.isActive;
    this.formError = '';
    this.showModal = true;
  }

  closeModal(): void {
    this.showModal = false;
    this.resetForm();
  }

  resetForm(): void {
    this.username = '';
    this.fullName = '';
    this.password = '';
    this.roleId = this.roles[0]?.id ?? null;
    this.isActive = true;
    this.formError = '';
  }

  saveUser(): void {
    const username = this.username.trim();
    const fullName = this.fullName.trim();

    if (!username) {
      this.formError = 'Username is required.';
      return;
    }

    if (!fullName) {
      this.formError = 'Full name is required.';
      return;
    }

    if (this.roleId == null) {
      this.formError = 'Select a role.';
      return;
    }

    if (this.editingUserId === null && !this.password.trim()) {
      this.formError = 'Password is required.';
      return;
    }

    if (this.password.trim() && this.password.trim().length < 6) {
      this.formError = 'Password must be at least 6 characters.';
      return;
    }

    this.formError = '';
    const roleId = this.roleId;

    if (this.editingUserId === null) {
      this.userManagementService.create({
        username,
        fullName,
        password: this.password.trim(),
        roleId
      }).subscribe({
        next: () => {
          this.toast.success('User created.');
          this.closeModal();
          this.loadUsers();
        },
        error: err => {
          this.formError = getApiErrorMessage(err, 'Unable to create user.');
          this.cdr.detectChanges();
        }
      });

      return;
    }

    const currentUsername = this.users.find(user => user.id === this.editingUserId)?.username;

    this.userManagementService.update(this.editingUserId, {
      username,
      fullName,
      password: this.password.trim() || undefined,
      roleId,
      isActive: this.isActive
    }).subscribe({
      next: updated => {
        if (currentUsername && currentUsername === this.authService.currentUser?.username) {
          this.authService.syncCurrentUser({
            username: updated.username,
            fullName: updated.fullName,
            roleId: updated.roleId,
            roleName: updated.roleName
          });
        }

        this.toast.success('User updated.');
        this.closeModal();
        this.loadUsers();
      },
      error: err => {
        this.formError = getApiErrorMessage(err, 'Unable to update user.');
        this.cdr.detectChanges();
      }
    });
  }

  async toggleStatus(user: ManagedUser): Promise<void> {
    const confirmed = await this.confirmModal.confirm({
      title: user.isActive ? 'Deactivate User' : 'Activate User',
      message: `Are you sure you want to ${user.isActive ? 'deactivate' : 'activate'} "${user.fullName}"?`,
      confirmText: user.isActive ? 'Deactivate' : 'Activate',
      cancelText: 'Cancel'
    });

    if (!confirmed) return;

    this.userManagementService.update(user.id, {
      username: user.username,
      fullName: user.fullName,
      roleId: user.roleId,
      isActive: !user.isActive
    }).subscribe({
      next: updated => {
        if (updated.username === this.authService.currentUser?.username) {
          this.authService.syncCurrentUser({
            username: updated.username,
            fullName: updated.fullName,
            roleId: updated.roleId,
            roleName: updated.roleName
          });
        }

        this.toast.success(`User ${updated.isActive ? 'activated' : 'deactivated'}.`);
        this.loadUsers();
      },
      error: err => {
        this.toast.error(getApiErrorMessage(err, 'Unable to update user status.'));
      }
    });
  }

  formatDate(value: string): string {
    return formatDateDisplay(value);
  }
}
