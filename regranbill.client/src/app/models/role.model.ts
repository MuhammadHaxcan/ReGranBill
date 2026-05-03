export interface Role {
  id: number;
  name: string;
  isSystem: boolean;
  isAdmin: boolean;
  pages: string[];
  userCount: number;
}

export interface CreateRoleRequest {
  name: string;
  pages: string[];
}

export interface UpdateRoleRequest {
  name: string;
  pages: string[];
}
