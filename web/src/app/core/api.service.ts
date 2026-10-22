import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AuditEntry,
  CreatePatientRequest,
  CreateUserRequest,
  DoseStatus,
  FhirExportResult,
  Immunization,
  PagedResult,
  Patient,
  PatientCalendar,
  PatientListItem,
  RegisterImmunizationRequest,
  ReminderItem,
  ReportSummary,
  User,
  Vaccine,
} from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  patients(search: string, overdueOnly: boolean, page: number, pageSize = 20): Observable<PagedResult<PatientListItem>> {
    const params = new HttpParams()
      .set('search', search)
      .set('overdueOnly', overdueOnly)
      .set('page', page)
      .set('pageSize', pageSize);
    return this.http.get<PagedResult<PatientListItem>>('/api/patients', { params });
  }

  patient(id: number): Observable<Patient> {
    return this.http.get<Patient>(`/api/patients/${id}`);
  }

  createPatient(body: CreatePatientRequest): Observable<Patient> {
    return this.http.post<Patient>('/api/patients', body);
  }

  calendar(patientId: number): Observable<PatientCalendar> {
    return this.http.get<PatientCalendar>(`/api/patients/${patientId}/calendar`);
  }

  immunizations(patientId: number): Observable<Immunization[]> {
    return this.http.get<Immunization[]>(`/api/patients/${patientId}/immunizations`);
  }

  registerImmunization(patientId: number, body: RegisterImmunizationRequest): Observable<Immunization> {
    return this.http.post<Immunization>(`/api/patients/${patientId}/immunizations`, body);
  }

  markEnteredInError(immunizationId: number): Observable<Immunization> {
    return this.http.post<Immunization>(`/api/immunizations/${immunizationId}/entered-in-error`, {});
  }

  myCalendar(): Observable<PatientCalendar> {
    return this.http.get<PatientCalendar>('/api/me/calendar');
  }

  myProfile(): Observable<Patient> {
    return this.http.get<Patient>('/api/me/profile');
  }

  vaccines(): Observable<Vaccine[]> {
    return this.http.get<Vaccine[]>('/api/vaccines');
  }

  reminders(status?: DoseStatus): Observable<ReminderItem[]> {
    const params = status ? new HttpParams().set('status', status) : undefined;
    return this.http.get<ReminderItem[]>('/api/reminders', { params });
  }

  sendReminders(): Observable<{ sent: number }> {
    return this.http.post<{ sent: number }>('/api/reminders/send', {});
  }

  report(from: string, to: string): Observable<ReportSummary> {
    const params = new HttpParams().set('from', from).set('to', to);
    return this.http.get<ReportSummary>('/api/reports/summary', { params });
  }

  fhirEverything(patientId: number): Observable<unknown> {
    return this.http.get<unknown>(`/fhir/Patient/${patientId}/$everything`);
  }

  fhirExport(patientId: number): Observable<FhirExportResult> {
    return this.http.post<FhirExportResult>(`/api/fhir/export/${patientId}`, {});
  }

  users(): Observable<User[]> {
    return this.http.get<User[]>('/api/admin/users');
  }

  createUser(body: CreateUserRequest): Observable<User> {
    return this.http.post<User>('/api/admin/users', body);
  }

  setUserActive(id: number, isActive: boolean): Observable<User> {
    return this.http.put<User>(`/api/admin/users/${id}/active`, { isActive });
  }

  audit(user: string, path: string, page: number): Observable<PagedResult<AuditEntry>> {
    const params = new HttpParams().set('user', user).set('path', path).set('page', page).set('pageSize', 50);
    return this.http.get<PagedResult<AuditEntry>>('/api/admin/audit', { params });
  }
}
