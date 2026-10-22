import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { errorMessage, formatAge } from '../core/labels';
import { CreatePatientRequest, PagedResult, PatientListItem } from '../core/models';

@Component({
  selector: 'app-patients',
  imports: [FormsModule, DatePipe, RouterLink],
  template: `
    <div class="page">
      <header class="page-head">
        <div>
          <h1>Pacjenci</h1>
          @if (result(); as r) {
            <p class="lead">{{ r.total }} {{ r.total === 1 ? 'pacjent' : 'pacjentów' }}{{ overdueOnly ? ' z zaległymi szczepieniami' : '' }}.</p>
          }
        </div>
        @if (auth.hasRole('Nurse')) {
          <div class="actions">
            <button [class.quiet]="showForm()" (click)="showForm.set(!showForm())">{{ showForm() ? 'Zamknij formularz' : 'Dodaj pacjenta' }}</button>
          </div>
        }
      </header>

      @if (showForm()) {
        <section class="panel new-patient">
          <h2>Nowy pacjent</h2>
          <p class="muted small">Datę urodzenia i płeć odczytamy z numeru PESEL.</p>
          @if (formError()) { <div class="message error" role="alert">{{ formError() }}</div> }
          <form class="form-grid" (ngSubmit)="create()">
            <label>Imię <input name="firstName" [(ngModel)]="form.firstName" required /></label>
            <label>Nazwisko <input name="lastName" [(ngModel)]="form.lastName" required /></label>
            <label>PESEL <input name="pesel" [(ngModel)]="form.pesel" maxlength="11" inputmode="numeric" required /></label>
            <label>Opiekun prawny <input name="guardian" [(ngModel)]="form.guardianName" /></label>
            <label>E-mail opiekuna <input name="email" type="email" [(ngModel)]="form.email" /></label>
            <label>Telefon <input name="phone" [(ngModel)]="form.phone" /></label>
            <div><button type="submit">Zapisz pacjenta</button></div>
          </form>
        </section>
      }

      <form class="filters row" (ngSubmit)="search(1)" role="search">
        <input class="search" name="q" [(ngModel)]="query" placeholder="Nazwisko, imię albo PESEL" aria-label="Szukaj pacjenta" />
        <button type="submit" class="quiet">Szukaj</button>
        <label class="check"><input type="checkbox" name="overdue" [(ngModel)]="overdueOnly" (ngModelChange)="search(1)" /> Tylko z zaległościami</label>
      </form>

      @if (error()) { <div class="message error" role="alert">{{ error() }}</div> }

      @if (result(); as r) {
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Pacjent</th>
                <th>PESEL</th>
                <th>Urodzenie</th>
                <th>Wiek</th>
                <th class="num">Podane</th>
                <th class="num">Zaległe</th>
                <th class="num">W ciągu 30 dni</th>
              </tr>
            </thead>
            <tbody>
              @for (p of r.items; track p.id) {
                <tr class="clickable" (click)="open(p.id)">
                  <td><a [routerLink]="['/patients', p.id]" (click)="$event.stopPropagation()">{{ p.lastName }} {{ p.firstName }}</a></td>
                  <td>{{ p.pesel }}</td>
                  <td>{{ p.birthDate | date: 'dd.MM.yyyy' }}</td>
                  <td>{{ age(p.ageInMonths) }}</td>
                  <td class="num">{{ p.completedCount }}</td>
                  <td class="num" [class.count-overdue]="p.overdueCount > 0" [class.zero]="p.overdueCount === 0">{{ p.overdueCount }}</td>
                  <td class="num" [class.count-soon]="p.dueSoonCount > 0" [class.zero]="p.dueSoonCount === 0">{{ p.dueSoonCount }}</td>
                </tr>
              } @empty {
                <tr><td colspan="7" class="muted">Nikt nie pasuje do wyszukiwania. Sprawdź pisownię albo wyczyść filtr.</td></tr>
              }
            </tbody>
          </table>
        </div>
        @if (pages(r) > 1) {
          <div class="row pager">
            <button class="quiet compact" [disabled]="r.page <= 1" (click)="search(r.page - 1)">Poprzednia</button>
            <span class="small">Strona {{ r.page }} z {{ pages(r) }}</span>
            <button class="quiet compact" [disabled]="r.page >= pages(r)" (click)="search(r.page + 1)">Następna</button>
          </div>
        }
      }
    </div>
  `,
  styles: `
    .new-patient { margin-bottom: 2rem; }
    .new-patient h2 { margin-bottom: .25rem; }
    .new-patient form { margin-top: 1rem; }
    .filters { margin-bottom: 1rem; }
    .search { flex: 1; min-width: 240px; max-width: 420px; }
    .pager { margin-top: 1rem; }
    td a { font-weight: 700; color: var(--ink); text-decoration: none; }
    td a:hover { text-decoration: underline; }
    td.zero { color: var(--rule-strong); }
  `,
})
export class PatientsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);

  protected query = '';
  protected overdueOnly = false;
  protected readonly result = signal<PagedResult<PatientListItem> | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly showForm = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected form: CreatePatientRequest = this.emptyForm();

  ngOnInit(): void {
    this.search(1);
  }

  protected search(page: number): void {
    this.error.set(null);
    this.api.patients(this.query, this.overdueOnly, page).subscribe({
      next: (r) => this.result.set(r),
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  protected create(): void {
    this.formError.set(null);
    const body: CreatePatientRequest = {
      ...this.form,
      email: this.form.email?.trim() || null,
      phone: this.form.phone?.trim() || null,
      guardianName: this.form.guardianName?.trim() || null,
    };
    this.api.createPatient(body).subscribe({
      next: (p) => this.router.navigate(['/patients', p.id]),
      error: (err) => this.formError.set(errorMessage(err)),
    });
  }

  protected open(id: number): void {
    this.router.navigate(['/patients', id]);
  }

  protected pages(r: PagedResult<unknown>): number {
    return Math.max(1, Math.ceil(r.total / r.pageSize));
  }

  protected age(months: number): string {
    return formatAge(months);
  }

  private emptyForm(): CreatePatientRequest {
    return { firstName: '', lastName: '', pesel: '', email: null, phone: null, guardianName: null };
  }
}
