import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ApiService } from '../core/api.service';
import { errorMessage } from '../core/labels';
import { Patient, PatientCalendar } from '../core/models';
import { ScheduleGridComponent } from '../shared/schedule-grid.component';

/** Parent's view: their own child's schedule only. */
@Component({
  selector: 'app-my-calendar',
  imports: [DatePipe, ScheduleGridComponent],
  template: `
    <div class="page">
      @if (error()) { <div class="message error" role="alert">{{ error() }}</div> }

      @if (profile(); as p) {
        <header class="page-head">
          <div>
            <h1>{{ p.firstName }} {{ p.lastName }}</h1>
            <p class="lead">Urodzenie {{ p.birthDate | date: 'dd.MM.yyyy' }}. Tu widać, które szczepienia dziecko już ma, a które są przed nim.</p>
          </div>
        </header>
      }

      @if (upcoming().length > 0) {
        <section class="reminder">
          <h2>Do umówienia w przychodni</h2>
          <ul>
            @for (e of upcoming(); track e.vaccineId + '-' + e.doseNumber) {
              <li>
                <span class="status {{ e.status }}">{{ e.status === 'Overdue' ? 'Po terminie' : 'Wkrótce' }}</span>
                {{ e.vaccineName }}, dawka {{ e.doseNumber }}, termin {{ e.dueDate | date: 'dd.MM.yyyy' }}
              </li>
            }
          </ul>
        </section>
      } @else if (calendar()) {
        <p class="message success">Wszystkie obowiązkowe szczepienia są na bieżąco.</p>
      }

      @if (calendar(); as c) {
        <app-schedule-grid [calendar]="c" />
      }
    </div>
  `,
  styles: `
    .reminder { background: var(--surface); border-left: 4px solid var(--soon); padding: 1rem 1.25rem; margin-bottom: 2rem; max-width: 760px; }
    .reminder h2 { font-size: 1.1rem; margin-bottom: .5rem; }
    ul { margin: 0; padding: 0; list-style: none; }
    li { padding: .35rem 0; display: flex; gap: 1rem; align-items: baseline; flex-wrap: wrap; }
    li .status { min-width: 6.5rem; }
  `,
})
export class MyCalendarComponent implements OnInit {
  private readonly api = inject(ApiService);

  protected readonly profile = signal<Patient | null>(null);
  protected readonly calendar = signal<PatientCalendar | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly upcoming = computed(() =>
    (this.calendar()?.entries ?? []).filter((e) => e.mandatory && (e.status === 'Overdue' || e.status === 'DueSoon')),
  );

  ngOnInit(): void {
    this.api.myProfile().subscribe({ next: (p) => this.profile.set(p), error: (e) => this.error.set(errorMessage(e)) });
    this.api.myCalendar().subscribe({ next: (c) => this.calendar.set(c), error: (e) => this.error.set(errorMessage(e)) });
  }
}
