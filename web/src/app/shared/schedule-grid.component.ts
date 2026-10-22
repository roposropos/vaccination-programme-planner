import { DatePipe } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';
import { STATUS_LABELS, formatAge, scheduleAge } from '../core/labels';
import { CalendarEntry, PatientCalendar } from '../core/models';

interface GridRow {
  vaccineId: number;
  code: string;
  name: string;
  disease: string;
  mandatory: boolean;
  cells: Map<number, CalendarEntry>;
}

/**
 * Same layout as the printed immunisation programme: a row per vaccine, a column per age.
 * Columns the child has already reached are shaded.
 */
@Component({
  selector: 'app-schedule-grid',
  imports: [DatePipe],
  template: `
    <div class="grid-wrap">
      <table class="grid">
        <caption class="visually-hidden">Kalendarz szczepień: szczepionki w wierszach, wiek w kolumnach</caption>
        <thead>
          <tr>
            <th scope="col" class="vaccine-col">Szczepionka</th>
            @for (age of ages(); track age) {
              <th scope="col" [class.reached]="age <= childAge()" [class.now]="age === lastReached()">
                {{ label(age) }}
              </th>
            }
          </tr>
        </thead>
        <tbody>
          @for (row of rows(); track row.vaccineId) {
            <tr>
              <th scope="row" class="vaccine-col">
                <span class="name">{{ row.name }}</span>
                <span class="disease">{{ row.disease }}@if (!row.mandatory) {, zalecane}</span>
              </th>
              @for (age of ages(); track age) {
                <td [class.reached]="age <= childAge()" [class.now]="age === lastReached()">
                  @if (row.cells.get(age); as e) {
                    @if (canRegister() && e.status !== 'Completed') {
                      <button
                        type="button"
                        class="dose {{ e.status }}"
                        [attr.aria-label]="describe(row, e)"
                        [title]="describe(row, e)"
                        (click)="register.emit(e)">
                        {{ e.doseNumber }}
                      </button>
                    } @else {
                      <span class="dose {{ e.status }}" [attr.aria-label]="describe(row, e)" [title]="describe(row, e)">{{ e.doseNumber }}</span>
                    }
                    <span class="when">
                      @if (e.administeredOn) { {{ e.administeredOn | date: 'dd.MM.yy' }} }
                      @else { {{ e.dueDate | date: 'dd.MM.yy' }} }
                    </span>
                  }
                </td>
              }
            </tr>
          }
        </tbody>
      </table>
    </div>

    <p class="legend small">
      <span class="status Completed">podana</span>
      <span class="status Overdue">zaległa</span>
      <span class="status DueSoon">w ciągu 30 dni</span>
      <span class="status Planned">zaplanowana</span>
      <span class="muted">Liczba w znaczniku to numer dawki, data pod nim to dzień podania albo termin.
        Zacieniowane kolumny: wiek, który dziecko już osiągnęło ({{ age(childAge()) }}).</span>
      @if (canRegister()) { <span class="muted">Kliknij niepodaną dawkę, żeby ją zarejestrować.</span> }
    </p>
  `,
  styles: `
    .visually-hidden { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); }
    .grid-wrap { overflow-x: auto; border: 1px solid var(--rule-strong); background: var(--surface); }
    table.grid { border-collapse: collapse; min-width: 100%; }
    .grid th, .grid td { border: 1px solid var(--rule); padding: .45rem .5rem; text-align: center; vertical-align: middle; }
    .grid thead th { background: var(--paper); font-size: .8rem; white-space: nowrap; color: var(--ink-soft); border-bottom: 2px solid var(--rule-strong); }
    .grid thead th.reached { color: var(--ink); }
    .grid .vaccine-col {
      position: sticky; left: 0; z-index: 1;
      text-align: left; background: var(--surface); min-width: 15rem; max-width: 17rem;
      border-right: 2px solid var(--rule-strong);
    }
    .grid thead .vaccine-col { background: var(--paper); }
    .name { display: block; font-weight: 700; font-size: .9rem; line-height: 1.25; }
    .disease { display: block; color: var(--ink-soft); font-size: .8rem; font-weight: 400; }
    td.reached { background: #eef1ef; }
    th.now, td.now { box-shadow: inset -3px 0 0 var(--ink); }
    td { min-width: 4.6rem; height: 3.6rem; }
    .when { display: block; font-size: .72rem; color: var(--ink-soft); margin-top: .2rem; }

    .dose {
      display: inline-grid; place-items: center;
      width: 1.9rem; height: 1.9rem; min-height: 0; padding: 0;
      font-weight: 700; font-size: .85rem; border-radius: 50%;
      border: 2px solid transparent; background: transparent; color: var(--ink);
    }
    .dose.Completed { background: var(--given); color: #fff; }
    .dose.Overdue { border-color: var(--overdue); color: var(--overdue); border-radius: 3px; background: var(--overdue-soft); }
    .dose.DueSoon { border-color: var(--soon); color: var(--soon); background: var(--soon-soft); }
    .dose.Planned { border-color: var(--planned); color: var(--planned); border-style: dashed; }
    button.dose:hover { outline: 2px solid var(--action); outline-offset: 2px; }

    .legend { display: flex; flex-wrap: wrap; gap: .5rem 1.25rem; margin-top: .75rem; align-items: baseline; }
    .legend .muted { flex-basis: 100%; }
  `,
})
export class ScheduleGridComponent {
  readonly calendar = input.required<PatientCalendar>();
  readonly canRegister = input(false);
  readonly register = output<CalendarEntry>();

  protected readonly ages = computed<number[]>(() =>
    [...new Set(this.calendar().entries.map((e) => e.ageInMonths))].sort((a, b) => a - b),
  );

  protected readonly childAge = computed(() => {
    const birth = new Date(this.calendar().birthDate);
    const today = new Date(this.calendar().today);
    let months = (today.getFullYear() - birth.getFullYear()) * 12 + today.getMonth() - birth.getMonth();
    if (today.getDate() < birth.getDate()) months--;
    return Math.max(0, months);
  });

  /** The last schedule column the child has reached, drawn with a heavier right edge. */
  protected readonly lastReached = computed(() => {
    const reached = this.ages().filter((a) => a <= this.childAge());
    return reached.length ? reached[reached.length - 1] : -1;
  });

  protected readonly rows = computed<GridRow[]>(() => {
    const byVaccine = new Map<number, GridRow>();
    for (const e of this.calendar().entries) {
      let row = byVaccine.get(e.vaccineId);
      if (!row) {
        row = { vaccineId: e.vaccineId, code: e.vaccineCode, name: e.vaccineName, disease: e.targetDisease, mandatory: e.mandatory, cells: new Map() };
        byVaccine.set(e.vaccineId, row);
      }
      row.cells.set(e.ageInMonths, e);
    }
    const firstAge = (r: GridRow) => Math.min(...r.cells.keys());
    return [...byVaccine.values()].sort((a, b) => firstAge(a) - firstAge(b) || a.code.localeCompare(b.code));
  });

  protected label(months: number): string {
    return scheduleAge(months);
  }

  protected age(months: number): string {
    return formatAge(months);
  }

  protected describe(row: GridRow, e: CalendarEntry): string {
    const when = e.administeredOn ? `podana ${e.administeredOn}, seria ${e.lotNumber}` : `termin ${e.dueDate}`;
    return `${row.name}, dawka ${e.doseNumber} z ${e.seriesDoses}: ${STATUS_LABELS[e.status].toLowerCase()}, ${when}`;
  }
}
