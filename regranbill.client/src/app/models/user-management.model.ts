export interface ManagedUser {
  id: number;
  username: string;
  fullName: string;
  roleId: number;
  roleName: string;
  isActive: boolean;
  createdAt: string;
}

export interface CreateManagedUserRequest {
  username: string;
  fullName: string;
  password: string;
  roleId: number;
}

export interface UpdateManagedUserRequest {
  username: string;
  fullName: string;
  password?: string;
  roleId: number;
  isActive: boolean;
}
