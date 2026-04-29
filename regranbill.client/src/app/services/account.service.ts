import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Account } from '../models/account.model';

@Injectable({
  providedIn: 'root',
})
export class AccountService {
  private url = '/api/accounts';

  constructor(private http: HttpClient) {}

  getAll(): Observable<Account[]> {
    return this.http.get<Account[]>(this.url);
  }

  getCustomers(): Observable<Account[]> {
    return this.http.get<Account[]>(`${this.url}/customers`);
  }

  getVendors(): Observable<Account[]> {
    return this.http.get<Account[]>(`${this.url}/vendors`);
  }

  getTransporters(): Observable<Account[]> {
    return this.http.get<Account[]>(`${this.url}/transporters`);
  }

  getProducts(): Observable<Account[]> {
    return this.http.get<Account[]>(`${this.url}/products`);
  }

  getJournalAccounts(): Observable<Account[]> {
    return this.http.get<Account[]>(`${this.url}/journal`);
  }

  getByCategory(categoryId: number): Observable<Account[]> {
    return this.http.get<Account[]>(`${this.url}/by-category/${categoryId}`);
  }

  add(account: Omit<Account, 'id'>): Observable<Account> {
    return this.http.post<Account>(this.url, account);
  }

  update(id: number, data: Partial<Account>): Observable<Account> {
    return this.http.put<Account>(`${this.url}/${id}`, data);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}
