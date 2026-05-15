import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Category } from '../models/category.model';

@Injectable({
  providedIn: 'root',
})
export class CategoryService {
  private url = '/api/categories';

  constructor(private http: HttpClient) {}

  getAll(): Observable<Category[]> {
    return this.http.get<Category[]>(this.url);
  }

  /**
   * Server-side filtered list. Returns only categories that contain at least one account
   * whose AccountType is in `accountTypes`, and (if provided) whose PartyRole is in `partyRoles`.
   */
  getFiltered(accountTypes: string[], partyRoles?: string[]): Observable<Category[]> {
    const params: string[] = [];
    if (accountTypes && accountTypes.length) {
      params.push('accountTypes=' + encodeURIComponent(accountTypes.join(',')));
    }
    if (partyRoles && partyRoles.length) {
      params.push('partyRoles=' + encodeURIComponent(partyRoles.join(',')));
    }
    const query = params.length ? '?' + params.join('&') : '';
    return this.http.get<Category[]>(`${this.url}/filtered${query}`);
  }

  add(name: string): Observable<Category> {
    return this.http.post<Category>(this.url, { name });
  }

  update(id: number, name: string): Observable<Category> {
    return this.http.put<Category>(`${this.url}/${id}`, { name });
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}
