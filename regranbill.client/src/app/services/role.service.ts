import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateRoleRequest, Role, UpdateRoleRequest } from '../models/role.model';

@Injectable({ providedIn: 'root' })
export class RoleService {
  private url = '/api/roles';

  constructor(private http: HttpClient) {}

  getAll(): Observable<Role[]> {
    return this.http.get<Role[]>(this.url);
  }

  getById(id: number): Observable<Role> {
    return this.http.get<Role>(`${this.url}/${id}`);
  }

  create(req: CreateRoleRequest): Observable<Role> {
    return this.http.post<Role>(this.url, req);
  }

  update(id: number, req: UpdateRoleRequest): Observable<Role> {
    return this.http.put<Role>(`${this.url}/${id}`, req);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}
