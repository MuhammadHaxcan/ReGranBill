export enum UserRole {
  Admin = 'Admin',
  Operator = 'Operator',
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  username: string;
  fullName: string;
  role: string;
}

export interface AppUser {
  username: string;
  fullName: string;
  role: UserRole;
  token: string;
}
