export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  username: string;
  fullName: string;
  roleId: number;
  roleName: string;
  isAdmin: boolean;
  pages: string[];
}

export interface AppUser {
  username: string;
  fullName: string;
  roleId: number;
  roleName: string;
  isAdmin: boolean;
  pages: string[];
  token: string;
}
