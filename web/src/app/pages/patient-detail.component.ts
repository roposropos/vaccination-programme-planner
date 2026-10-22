import { DatePipe, JsonPipe } from '@angular/common';
import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { errorMessage, todayIso } from '../core/labels';
import {
  CalendarEntry,
  FhirExportResult,
  Immunization,
  Patient,
  PatientCalendar,
  RegisterImmunizationRequest,
  Vaccine,
} from '../core/models';
import { ScheduleGridComponent } from '../shared/schedule-grid.component';

@Component({
  selector: 'app-patient-detail',
  imports: [FormsModule, DatePipe, JsonPipe, RouterLink, ScheduleGridComponent],
  template: `
    <div class="page">
      <a routerLink="/patients" class="small">Wróć do listy pacjentów</a>

      @if (error()) { <div class="message error" role="alert">{{ error() }}</div> }
      @if (success()) { <div class="message success" role="status">{{ success() }}</div> }

      @if (patient(); as p) {
        <header class="patient-head">
          <h1>{{ p.firstName }} {{ p.lastName }}</h1>
          <dl class="facts">
            <div><dt>PESEL</dt><dd>{{ p.pesel }}</dd></div>
            <div><dt>Urodzenie</dt><dd>{{ p.birthDate | date: 'dd.MM.yyyy' }}</dd></div>
            <div><dt>Płeć</dt><dd>{{ p.sex === 'Female' ? 'dziewczynka' : 'chłopiec' }}</dd></div>
            <div><dt>Opiekun</dt><dd>{{ p.guardianName ?? 'brak' }}</dd></div>
            <div><dt>Kontakt</dt><dd>{{ p.phone ?? '' }} @if (p.email) { <a [href]="'mailto:' + p.email">{{ p.email }}</a> }</dd></div>
          </dl>
        </header>
      }

      @if (calendar(); as c) {
        <section class="section">
          <h2>Kalendarz szczepień</h2>
          <p class="summary">{{ summary(c) }}</p>

          @if (todo().length > 0) {
            <ul class="todo">
              @for (e of todo(); track e.vaccineId + '-' + e.doseNumber) {
                <li>
                  <span class="status {{ e.status }}">{{ e.status === 'Overdue' ? 'Zaległa' : 'Wkrótce' }}</span>
                  <span>{{ e.vaccineName }}, dawka {{ e.doseNumber }}, termin {{ e.dueDate | date: 'dd.MM.yyyy' }}</span>
                  @if (isNurse()) { <button class="link" (click)="openForm(e)">Zarejestruj</button> }
                </li>
              }
            </ul>
          }

          @if (showForm()) {
            <form class="panel register" id="register" (ngSubmit)="register()">
              <h3>Zarejestruj szczepienie</h3>
              <div class="form-grid">
                <label>Szczepionka
                  <select name="vaccine" [(ngModel)]="form.vaccineId" (ngModelChange)="form.doseNumber = 1">
                    @for (v of vaccines(); track v.id) { <option [ngValue]="v.id">{{ v.name }}</option> }
                  </select>
                </label>
                <label>Dawka
                  <select name="dose" [(ngModel)]="form.doseNumber">
                    @for (d of selectedVaccineDoses(); track d.doseNumber) { <option [ngValue]="d.doseNumber">{{ d.doseNumber }}</option> }
                  </select>
                </label>
                <label>Data podania <input name="date" type="date" [(ngModel)]="form.administeredOn" [max]="today" required /></label>
                <label>Numer serii <input name="lot" [(ngModel)]="form.lotNumber" required /></label>
                <label>Miejsce podania
                  <select name="site" [(ngModel)]="form.site">
                    <option [ngValue]="null">nie podano</option>
                    @for (site of sites; track site) { <option [value]="site">{{ site }}</option> }
                  </select>
                </label>
                <label>Uwagi <input name="notes" [(ngModel)]="form.notes" /></label>
              </div>
              <div class="row form-actions">
                <button type="submit" [disabled]="busy()">Zapisz szczepienie</button>
                <button type="button" class="quiet" (click)="showForm.set(false)">Anuluj</button>
              </div>
            </form>
          }

          <app-schedule-grid [calendar]="c" [canRegister]="isNurse()" (register)="openForm($event)" />
        </section>
      }

      <section class="section">
        <h2>Historia szczepień</h2>
        <div class="table-wrap">
          <table>
            <thead>
              <tr><th>Data</th><th>Szczepionka</th><th class="num">Dawka</th><th>Seria</th><th>Miejsce</th><th>Wykonał(a)</th><th></th></tr>
            </thead>
            <tbody>
              @for (i of immunizations(); track i.id) {
                <tr [class.cancelled]="i.status === 'EnteredInError'">
                  <td>{{ i.administeredOn | date: 'dd.MM.yyyy' }}</td>
                  <td>{{ i.vaccineName }}</td>
                  <td class="num">{{ i.doseNumber }}</td>
                  <td>{{ i.lotNumber }}</td>
                  <td>{{ i.site ?? '' }}</td>
                  <td>{{ i.performedBy ?? '' }}</td>
                  <td class="num">
                    @if (i.status === 'EnteredInError') { <span class="small">wpis błędny</span> }
                    @else if (isNurse()) { <button class="link small" (click)="markError(i)">Oznacz jako błędny</button> }
                  </td>
                </tr>
              } @empty {
                <tr><td colspan="7" class="muted">Pacjent nie ma jeszcze zarejestrowanych szczepień.</td></tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="section">
        <h2>HL7 FHIR</h2>
        <p class="muted fhir-note">
          Dane pacjenta jako zasoby Patient, Practitioner, Immunization i ImmunizationRecommendation.
          Eksport zapisuje je na serwerze HAPI FHIR, a ponowny eksport aktualizuje te same zasoby.
        </p>
        <div class="row">
          <button class="quiet" (click)="toggleFhir()">{{ fhir() ? 'Ukryj zasoby' : 'Pokaż zasoby FHIR' }}</button>
          <button [disabled]="exporting()" (click)="exportFhir()">{{ exporting() ? 'Eksportowanie…' : 'Eksportuj do HAPI FHIR' }}</button>
        </div>
        @if (exportResult(); as r) {
          <div class="message export" [class.success]="r.success" [class.error]="!r.success" role="status">
            {{ r.message }} Zasoby: {{ r.resourceCount }}.
          </div>
        }
        @if (fhir()) { <pre class="json">{{ fhir() | json }}</pre> }
      </section>
    </div>
  `,
  styles: `
    .patient-head { margin-top: 1rem; }
    .facts { display: flex; flex-wrap: wrap; gap: .5rem 2.5rem; margin: 1rem 0 0; }
    .facts dt { font-size: .8rem; color: var(--ink-soft); }
    .facts dd { margin: 0; font-weight: 700; }
    .summary { font-size: 1.05rem; max-width: 70ch; }
    .todo { list-style: none; padding: 0; margin: 1rem 0 1.5rem; border-top: 1px solid var(--rule); }
    .todo li { display: flex; gap: 1rem; align-items: baseline; flex-wrap: wrap; padding: .55rem 0; border-bottom: 1px solid var(--rule); }
    .todo .status { min-width: 6rem; }
    .register { margin: 1.25rem 0 1.5rem; border-left: 4px solid var(--action); }
    .register h3 { margin-bottom: 1rem; }
    .form-actions { margin-top: 1.25rem; }
    app-schedule-grid { display: block; margin-top: 1.25rem; }
    tr.cancelled td:not(:last-child) { color: var(--ink-soft); text-decoration: line-through; }
    .fhir-note { margin-bottom: 1rem; max-width: 70ch; }
    .export { margin: 1rem 0 0; }
  `,
})
export class PatientDetailComponent implements OnInit {
  /** Route parameter :id, bound through withComponentInputBinding. */
  readonly id = input.required<string>();

  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);

  protected readonly patient = signal<Patient | null>(null);
  protected readonly calendar = signal<PatientCalendar | null>(null);
  protected readonly immunizations = signal<Immunization[]>([]);
  protected readonly vaccines = signal<Vaccine[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly success = signal<string | null>(null);

  protected readonly showForm = signal(false);
  protected readonly busy = signal(false);
  protected readonly today = todayIso();
  protected form: RegisterImmunizationRequest = this.emptyForm();

  protected readonly fhir = signal<unknown | null>(null);
  protected readonly exporting = signal(false);
  protected readonly exportResult = signal<FhirExportResult | null>(null);

  protected readonly isNurse = computed(() => this.auth.hasRole('Nurse'));

  protected readonly sites = ['lewe ramię', 'prawe ramię', 'lewe udo', 'prawe udo', 'doustnie', 'śródskórnie'];

  /** Overdue doses first, then the ones due in the next 30 days. */
  protected readonly todo = computed(() =>
    (this.calendar()?.entries ?? [])
      .filter((e) => e.status === 'Overdue' || e.status === 'DueSoon')
      .sort((a, b) => (a.status === b.status ? a.dueDate.localeCompare(b.dueDate) : a.status === 'Overdue' ? -1 : 1)),
  );

  protected summary(c: PatientCalendar): string {
    const total = c.entries.length;
    const head = `Podano ${c.completed} z ${total} dawek z kalendarza.`;
    if (c.overdue === 0 && c.dueSoon === 0) return `${head} Nic nie jest zaległe i w ciągu 30 dni nie przypada żadna dawka.`;
    const parts: string[] = [];
    if (c.overdue > 0) parts.push(`${c.overdue} zaległe`);
    if (c.dueSoon > 0) parts.push(`${c.dueSoon} w ciągu 30 dni`);
    return `${head} Do zrobienia: ${parts.join(', ')}.`;
  }

  private get patientId(): number {
    return Number(this.id());
  }

  ngOnInit(): void {
    this.api.vaccines().subscribe((v) => this.vaccines.set(v));
    this.load();
  }

  protected selectedVaccineDoses() {
    return this.vaccines().find((v) => v.id === this.form.vaccineId)?.doses ?? [];
  }

  protected openForm(entry?: CalendarEntry): void {
    this.form = this.emptyForm();
    if (entry) {
      this.form.vaccineId = entry.vaccineId;
      this.form.doseNumber = entry.doseNumber;
    }
    this.success.set(null);
    this.showForm.set(true);
    setTimeout(() => document.getElementById('register')?.scrollIntoView({ behavior: 'smooth' }));
  }

  protected register(): void {
    this.busy.set(true);
    this.error.set(null);
    const body: RegisterImmunizationRequest = {
      ...this.form,
      lotNumber: this.form.lotNumber.trim(),
      notes: this.form.notes?.trim() || null,
    };
    this.api.registerImmunization(this.patientId, body).subscribe({
      next: (i) => {
        this.success.set(`Zapisano szczepienie: ${i.vaccineName}, dawka ${i.doseNumber}.`);
        this.showForm.set(false);
        this.busy.set(false);
        this.load();
      },
      error: (err) => {
        this.error.set(errorMessage(err));
        this.busy.set(false);
      },
    });
  }

  protected markError(i: Immunization): void {
    if (!confirm(`Oznaczyć wpis „${i.vaccineName}, dawka ${i.doseNumber}” jako błędny?`)) return;
    this.api.markEnteredInError(i.id).subscribe({
      next: () => {
        this.success.set('Wpis oznaczono jako błędny. Dawka znowu jest w kalendarzu jako niepodana.');
        this.load();
      },
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  protected toggleFhir(): void {
    if (this.fhir()) {
      this.fhir.set(null);
      return;
    }
    this.api.fhirEverything(this.patientId).subscribe({
      next: (bundle) => this.fhir.set(bundle),
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  protected exportFhir(): void {
    this.exporting.set(true);
    this.exportResult.set(null);
    this.api.fhirExport(this.patientId).subscribe({
      next: (r) => {
        this.exportResult.set(r);
        this.exporting.set(false);
      },
      error: (err) => {
        // a 502 still carries the export result with the reason
        this.exportResult.set(err?.error?.message ? (err.error as FhirExportResult) : null);
        if (!err?.error?.message) this.error.set(errorMessage(err));
        this.exporting.set(false);
      },
    });
  }

  private load(): void {
    forkJoin({
      patient: this.api.patient(this.patientId),
      calendar: this.api.calendar(this.patientId),
      immunizations: this.api.immunizations(this.patientId),
    }).subscribe({
      next: ({ patient, calendar, immunizations }) => {
        this.patient.set(patient);
        this.calendar.set(calendar);
        this.immunizations.set(immunizations);
        if (this.fhir()) this.fhir.set(null);
      },
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  private emptyForm(): RegisterImmunizationRequest {
    return {
      vaccineId: this.vaccines()[0]?.id ?? 1,
      doseNumber: 1,
      administeredOn: todayIso(),
      lotNumber: '',
      site: null,
      notes: null,
    };
  }
}
