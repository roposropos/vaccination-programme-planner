import { Routes } from '@angular/router';
import { homeRedirect, roleGuard } from './core/auth.guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./pages/login.component').then((m) => m.LoginComponent) },
  {
    path: 'patients',
    canActivate: [roleGuard('Nurse', 'Doctor')],
    loadComponent: () => import('./pages/patients.component').then((m) => m.PatientsComponent),
  },
  {
    path: 'patients/:id',
    canActivate: [roleGuard('Nurse', 'Doctor')],
    loadComponent: () => import('./pages/patient-detail.component').then((m) => m.PatientDetailComponent),
  },
  {
    path: 'reminders',
    canActivate: [roleGuard('Nurse', 'Doctor')],
    loadComponent: () => import('./pages/reminders.component').then((m) => m.RemindersComponent),
  },
  {
    path: 'reports',
    canActivate: [roleGuard('Nurse', 'Doctor')],
    loadComponent: () => import('./pages/reports.component').then((m) => m.ReportsComponent),
  },
  {
    path: 'vaccines',
    canActivate: [roleGuard()],
    loadComponent: () => import('./pages/vaccines.component').then((m) => m.VaccinesComponent),
  },
  {
    path: 'my-calendar',
    canActivate: [roleGuard('Patient')],
    loadComponent: () => import('./pages/my-calendar.component').then((m) => m.MyCalendarComponent),
  },
  {
    path: 'admin/users',
    canActivate: [roleGuard('Admin')],
    loadComponent: () => import('./pages/admin-users.component').then((m) => m.AdminUsersComponent),
  },
  {
    path: 'admin/audit',
    canActivate: [roleGuard('Admin')],
    loadComponent: () => import('./pages/admin-audit.component').then((m) => m.AdminAuditComponent),
  },
  { path: '', pathMatch: 'full', canActivate: [homeRedirect], children: [] },
  { path: '**', redirectTo: '' },
];
