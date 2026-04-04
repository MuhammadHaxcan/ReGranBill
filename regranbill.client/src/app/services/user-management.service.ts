import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CreateManagedUserRequest, ManagedUser, UpdateManagedUserRequest } from '../models/user-management.model';

@Injectable({
  providedIn: 'root'
})
export class UserManagementService {
  constructor(private http: HttpClient) {}

  getAll(): Observable<ManagedUser[]> {
    return this.http.get<ManagedUser[]>('/api/users');
  }

  create(request: CreateManagedUserRequest): Observable<ManagedUser> {
    return this.http.post<ManagedUser>('/api/users', request);
  }

  update(id: number, request: UpdateManagedUserRequest): Observable<ManagedUser> {
    return this.http.put<ManagedUser>(`/api/users/${id}`, request);
  }
}
