import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  ApplyFormulationResponse,
  CreateFormulationRequest,
  FormulationDto
} from '../models/formulation.model';

@Injectable({ providedIn: 'root' })
export class FormulationService {
  private url = '/api/formulations';

  constructor(private http: HttpClient) {}

  getAll(): Observable<FormulationDto[]> {
    return this.http.get<FormulationDto[]>(this.url);
  }

  getById(id: number): Observable<FormulationDto> {
    return this.http.get<FormulationDto>(`${this.url}/${id}`);
  }

  create(request: CreateFormulationRequest): Observable<FormulationDto> {
    return this.http.post<FormulationDto>(this.url, request);
  }

  update(id: number, request: CreateFormulationRequest): Observable<FormulationDto> {
    return this.http.put<FormulationDto>(`${this.url}/${id}`, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }

  apply(id: number, totalInputKg: number): Observable<ApplyFormulationResponse> {
    return this.http.post<ApplyFormulationResponse>(`${this.url}/${id}/apply`, { totalInputKg });
  }
}
