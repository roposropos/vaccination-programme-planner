import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../core/api.service';
import { ROLE_LABELS, errorMessage } from '../core/labels';
import { CreateUserRequest, Role, User } from '../core/models';

@Component({
  selector: 'app-admin-users',
  imports: [FormsModule],
  template: `
    <div class="page">
      <header class="page-head">
        <div>
          <h1>Konta</h1>
          <p class="lead">Administrator zakłada konta i nadaje role. Danych medycznych pacjentów nie widzi.</p>
        </div>
      </header>

      @if (error()) { <div class="message error" role="alert">{{ error() }}</div> }

      <div class="table-wrap">
        <table>
          <thead><tr><th>E-mail</th><th>Imię i nazwisko</th><th>Rola</th><th>Stan</th><th></th></tr></thead>
          <tbody>
            @for (u of users(); track u.id) {
              <tr>
                <td>{{ u.email }}</td>
                <td>{{ u.displayName }}</td>
                <td>{{ roleLabels[u.role] }}@if (u.patientId) { <span class="muted small"> (kartoteka {{ u.patientId }})</span> }</td>
                <td>{{ u.isActive ? 'aktywne' : 'zablokowane' }}</td>
                <td class="num"><button class="link" (click)="toggle(u)">{{ u.isActive ? 'Zablokuj' : 'Odblokuj' }}</button></td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      <section class="section">
        <h2>Nowe konto</h2>
        <form class="form-grid" (ngSubmit)="create()">
          <label>E-mail <input name="email" type="email" [(ngModel)]="form.email" required /></label>
          <label>Imię i nazwisko <input name="name" [(ngModel)]="form.displayName" required /></label>
          <label>Rola
            <select name="role" [(ngModel)]="form.role">
              @for (r of roles; track r) { <option [ngValue]="r">{{ roleLabels[r] }}</option> }
            </select>
          </label>
          @if (form.role === 'Patient') {
            <label>Numer kartoteki pacjenta <input name="patientId" type="number" [(ngModel)]="form.patientId" /></label>
          }
          <label>Hasło, co najmniej 8 znaków <input name="password" type="password" [(ngModel)]="form.password" required /></label>
          <div><button type="submit">Utwórz konto</button></div>
        </form>
      </section>
    </div>
  `,
})
export class AdminUsersComponent implements OnInit {
  private readonly api = inject(ApiService);
  protected readonly roleLabels = ROLE_LABELS;
  protected readonly roles: Role[] = ['Nurse', 'Doctor', 'Patient', 'Admin'];

  protected readonly users = signal<User[]>([]);
  protected readonly error = signal<string | null>(null);
  protected form: CreateUserRequest = this.emptyForm();

  ngOnInit(): void {
    this.load();
  }

  protected create(): void {
    this.error.set(null);
    const body: CreateUserRequest = { ...this.form, patientId: this.form.role === 'Patient' ? this.form.patientId : null };
    this.api.createUser(body).subscribe({
      next: () => {
        this.form = this.emptyForm();
        this.load();
      },
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  protected toggle(u: User): void {
    this.api.setUserActive(u.id, !u.isActive).subscribe({
      next: () => this.load(),
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  private load(): void {
    this.api.users().subscribe({ next: (u) => this.users.set(u), error: (e) => this.error.set(errorMessage(e)) });
  }

  private emptyForm(): CreateUserRequest {
    return { email: '', displayName: '', role: 'Nurse', password: '', patientId: null };
  }
}
