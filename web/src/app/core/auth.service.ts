import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { LoginResponse, Role, User } from './models';

const STORAGE_KEY = 'vaxplan.session';

interface Session {
  token: string;
  expiresAt: string;
  user: User;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly session = signal<Session | null>(this.restore());

  readonly user = computed(() => this.session()?.user ?? null);
  readonly isLoggedIn = computed(() => this.session() !== null);

  get token(): string | null {
    return this.session()?.token ?? null;
  }

  hasRole(...roles: Role[]): boolean {
    const user = this.user();
    return !!user && roles.includes(user.role);
  }

  login(email: string, password: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>('/api/auth/login', { email, password }).pipe(
      tap((res) => {
        const session: Session = { token: res.token, expiresAt: res.expiresAt, user: res.user };
        this.session.set(session);
        try {
          localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
        } catch {
          // storage blocked (e.g. private mode): keep the session in memory only
        }
      }),
    );
  }

  logout(): void {
    this.session.set(null);
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // nothing to remove
    }
    this.router.navigateByUrl('/login');
  }

  homeUrl(): string {
    switch (this.user()?.role) {
      case 'Patient':
        return '/my-calendar';
      case 'Admin':
        return '/admin/users';
      default:
        return '/patients';
    }
  }

  private restore(): Session | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return null;
      const session = JSON.parse(raw) as Session;
      return new Date(session.expiresAt) > new Date() ? session : null;
    } catch {
      return null;
    }
  }
}
