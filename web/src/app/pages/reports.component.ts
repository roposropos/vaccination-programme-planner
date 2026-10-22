import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../core/api.service';
import { errorMessage, todayIso } from '../core/labels';
import { ReportSummary } from '../core/models';

@Component({
  selector: 'app-reports',
  imports: [FormsModule, DatePipe, DecimalPipe],
  template: `
    <div class="page">
      <header class="page-head">
        <div>
          <h1>Realizacja programu szczepień</h1>
          <p class="lead">Ile należnych dawek zostało podanych, w podziale na szczepionkę i dawkę.</p>
        </div>
        <div class="actions"><button class="quiet" type="button" (click)="print()">Drukuj</button></div>
      </header>

      <form class="row period" (ngSubmit)="load()">
        <label>Szczepienia od <input type="date" name="from" [(ngModel)]="from" /></label>
        <label>do <input type="date" name="to" [(ngModel)]="to" /></label>
        <button type="submit" class="quiet">Przelicz</button>
      </form>

      @if (error()) { <div class="message error" role="alert">{{ error() }}</div> }

      @if (report(); as r) {
        <p class="summary">
          Na dzień {{ r.generatedOn | date: 'd MMMM yyyy' }} przychodnia ma {{ r.patients }} pacjentów w programie.
          Podano <strong>{{ r.overallCoveragePercent | number: '1.1-1' }}%</strong> dawek, których termin już minął.
          {{ r.patientsWithOverdue }} pacjentów ma zaległości (łącznie {{ r.overdueDoses }} dawek),
          a w ciągu 30 dni przypada {{ r.dueSoonDoses }} kolejnych.
          Od {{ r.periodFrom | date: 'dd.MM.yyyy' }} do {{ r.periodTo | date: 'dd.MM.yyyy' }}
          zarejestrowano {{ r.immunizationsInPeriod }} szczepień.
        </p>

        <div class="table-wrap">
          <table>
            <thead>
              <tr><th>Szczepionka</th><th class="num">Dawka</th><th class="num">Należne</th><th class="num">Podane</th><th>Wyszczepialność</th></tr>
            </thead>
            <tbody>
              @for (c of r.coverage; track c.vaccineCode + c.doseNumber) {
                <tr [class.low]="c.coveragePercent < 90">
                  <td>{{ c.vaccineName }}</td>
                  <td class="num">{{ c.doseNumber }}</td>
                  <td class="num">{{ c.eligible }}</td>
                  <td class="num">{{ c.completed }}</td>
                  <td>
                    <div class="row coverage">
                      <div class="meter" aria-hidden="true"><span [style.width.%]="c.coveragePercent"></span></div>
                      <span>{{ c.coveragePercent | number: '1.1-1' }}%</span>
                    </div>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <p class="muted small note">Wiersze poniżej 90% są wyróżnione.</p>
      }
    </div>
  `,
  styles: `
    .period { margin-bottom: 1.5rem; align-items: flex-end; }
    .summary { font-size: 1.05rem; max-width: 72ch; margin-bottom: 1.5rem; line-height: 1.6; }
    .coverage { gap: .6rem; flex-wrap: nowrap; }
    tr.low td { color: var(--overdue); }
    tr.low .meter > span { background: var(--overdue); }
    .note { margin-top: .5rem; }
  `,
})
export class ReportsComponent implements OnInit {
  private readonly api = inject(ApiService);

  protected to = todayIso();
  protected from = `${Number(this.to.slice(0, 4)) - 1}${this.to.slice(4)}`;
  protected readonly report = signal<ReportSummary | null>(null);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.error.set(null);
    this.api.report(this.from, this.to).subscribe({
      next: (r) => this.report.set(r),
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  protected print(): void {
    window.print();
  }
}
