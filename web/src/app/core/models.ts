export type Role = 'Patient' | 'Nurse' | 'Doctor' | 'Admin';
export type DoseStatus = 'Completed' | 'Overdue' | 'DueSoon' | 'Planned';
export type ImmunizationStatus = 'Completed' | 'EnteredInError';
export type Sex = 'Female' | 'Male';

export interface User {
  id: number;
  email: string;
  displayName: string;
  role: Role;
  patientId: number | null;
  isActive: boolean;
}

export interface LoginResponse {
  token: string;
  expiresAt: string;
  user: User;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface PatientListItem {
  id: number;
  firstName: string;
  lastName: string;
  pesel: string;
  birthDate: string;
  ageInMonths: number;
  completedCount: number;
  overdueCount: number;
  dueSoonCount: number;
}

export interface Patient {
  id: number;
  firstName: string;
  lastName: string;
  pesel: string;
  birthDate: string;
  sex: Sex;
  email: string | null;
  phone: string | null;
  guardianName: string | null;
}

export interface CreatePatientRequest {
  firstName: string;
  lastName: string;
  pesel: string;
  email: string | null;
  phone: string | null;
  guardianName: string | null;
}

export interface CalendarEntry {
  vaccineId: number;
  vaccineCode: string;
  vaccineName: string;
  targetDisease: string;
  mandatory: boolean;
  doseNumber: number;
  seriesDoses: number;
  ageInMonths: number;
  description: string | null;
  dueDate: string;
  status: DoseStatus;
  immunizationId: number | null;
  administeredOn: string | null;
  lotNumber: string | null;
}

export interface PatientCalendar {
  patientId: number;
  patientName: string;
  birthDate: string;
  today: string;
  completed: number;
  overdue: number;
  dueSoon: number;
  planned: number;
  entries: CalendarEntry[];
}

export interface Immunization {
  id: number;
  patientId: number;
  vaccineId: number;
  vaccineCode: string;
  vaccineName: string;
  doseNumber: number;
  administeredOn: string;
  lotNumber: string;
  site: string | null;
  notes: string | null;
  performedBy: string | null;
  status: ImmunizationStatus;
  recordedAt: string;
}

export interface RegisterImmunizationRequest {
  vaccineId: number;
  doseNumber: number;
  administeredOn: string;
  lotNumber: string;
  site: string | null;
  notes: string | null;
}

export interface ScheduleDose {
  doseNumber: number;
  ageInMonths: number;
  description: string | null;
}

export interface Vaccine {
  id: number;
  code: string;
  name: string;
  targetDisease: string;
  cvxCode: string;
  icd10Code: string;
  mandatory: boolean;
  doses: ScheduleDose[];
}

export interface ReminderItem {
  patientId: number;
  patientName: string;
  guardianName: string | null;
  email: string | null;
  phone: string | null;
  vaccineCode: string;
  vaccineName: string;
  doseNumber: number;
  dueDate: string;
  status: DoseStatus;
  daysFromToday: number;
}

export interface VaccineCoverage {
  vaccineCode: string;
  vaccineName: string;
  doseNumber: number;
  eligible: number;
  completed: number;
  coveragePercent: number;
}

export interface ReportSummary {
  generatedOn: string;
  periodFrom: string;
  periodTo: string;
  patients: number;
  patientsUpToDate: number;
  patientsWithOverdue: number;
  overdueDoses: number;
  dueSoonDoses: number;
  immunizationsInPeriod: number;
  overallCoveragePercent: number;
  coverage: VaccineCoverage[];
}

export interface FhirExportResult {
  success: boolean;
  statusCode: number | null;
  resourceCount: number;
  server: string;
  message: string;
}

export interface AuditEntry {
  id: number;
  timestamp: string;
  userEmail: string | null;
  role: string | null;
  method: string;
  path: string;
  statusCode: number;
  ipAddress: string | null;
}

export interface CreateUserRequest {
  email: string;
  displayName: string;
  role: Role;
  password: string;
  patientId: number | null;
}
