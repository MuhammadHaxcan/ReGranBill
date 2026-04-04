import { UserRole } from './auth.model';

export interface ManagedUser {
  id: number;
  username: string;
  fullName: string;
  role: UserRole;
  isActive: boolean;
  createdAt: string;
}

export interface CreateManagedUserRequest {
  username: string;
  fullName: string;
  password: string;
  role: UserRole;
}

export interface UpdateManagedUserRequest {
  username: string;
  fullName: string;
  password?: string;
  role: UserRole;
  isActive: boolean;
}
