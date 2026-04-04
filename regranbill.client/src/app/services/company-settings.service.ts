import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { CompanySettings } from '../models/company-settings.model';

@Injectable({
  providedIn: 'root'
})
export class CompanySettingsService {
  private readonly url = '/api/company-settings';

  constructor(private http: HttpClient) {}

  getSettings(): Observable<CompanySettings> {
    return this.http.get<CompanySettings>(this.url);
  }

  updateSettings(formData: FormData): Observable<CompanySettings> {
    return this.http.put<CompanySettings>(this.url, formData);
  }

  getLogo(): Observable<Blob> {
    return this.http.get(`${this.url}/logo`, { responseType: 'blob' });
  }
}
