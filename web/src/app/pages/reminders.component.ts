import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { STATUS_LABELS, errorMessage } from '../core/labels';
import { DoseStatus, ReminderItem } from '../core/models';

@Component({
  selector: 'app-reminders',
  imports: [DatePipe, RouterLink],
  template: `
    <div class="page">
      <header class="page-head">
        <div>
          <h1>Przypomnienia</h1>
          <p class="lead">
            Obowiązkowe dawki zaległe i przypadające w ciągu 30 dni. Raz na dobę system sam wysyła
            rodzicom jeden zbiorczy e-mail; przycisk obok robi to od razu.
          </p>
        </div>
        @if (auth.hasRole('Nurse')) {
          <div class="actions"><button (click)="send()" [disabled]="sending()">Wyślij e-maile teraz</button></div>
        }
      </header>

      @if (message()) { <div class="message success" role="status">{{ message() }}</div> }
      @if (error()) { <div class="message error" role="alert">{{ error() }}</div> }

      <div class="row tabs" role="group" aria-label="Filtr">
        <button class="compact" [class.quiet]="status() !== null" (click)="load(null)">Wszystkie</button>
        <button class="compact" [class.quiet]="status() !== 'Overdue'" (click)="load('Overdue')">Zaległe</button>
        <button class="compact" [class.quiet]="status() !== 'DueSoon'" (click)="load('DueSoon')">W ciągu 30 dni</button>
        <span class="muted small">{{ items().length }} pozycji</span>
      </div>

      <div class="table-wrap">
        <table>
          <thead>
            <tr><th>Pacjent</th><th>Szczepienie</th><th class="num">Dawka</th><th>Termin</th><th>Status</th><th>Kontakt z opiekunem</th></tr>
          </thead>
          <tbody>
            @for (r of items(); track r.patientId + r.vaccineCode + r.doseNumber) {
              <tr>
                <td><a [routerLink]="['/patients', r.patientId]">{{ r.patientName }}</a></td>
                <td>{{ r.vaccineName }}</td>
                <td class="num">{{ r.doseNumber }}</td>
                <td>{{ r.dueDate | date: 'dd.MM.yyyy' }} <span class="muted small">{{ daysLabel(r.daysFromToday) }}</span></td>
                <td><span class="status {{ r.status }}">{{ labels[r.status] }}</span></td>
                <td class="small">{{ r.guardianName }}<br />{{ r.phone }}</td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="muted">Nie ma zaległych ani zbliżających się dawek.</td></tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `,
  styles: `.tabs { margin-bottom: 1rem; } td a { font-weight: 700; color: var(--ink); }`,
})
export class RemindersComponent implements OnInit {
  private readonly api = inject(ApiService);
  protected readonly auth = inject(AuthService);
  protected readonly labels = STATUS_LABELS;

  protected readonly items = signal<ReminderItem[]>([]);
  protected readonly status = signal<DoseStatus | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly message = signal<string | null>(null);
  protected readonly sending = signal(false);

  ngOnInit(): void {
    this.load(null);
  }

  protected load(status: DoseStatus | null): void {
    this.status.set(status);
    this.api.reminders(status ?? undefined).subscribe({
      next: (items) => this.items.set(items),
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  protected daysLabel(days: number): string {
    if (days === 0) return '(dzisiaj)';
    if (days === -1) return '(wczoraj)';
    if (days === 1) return '(jutro)';
    if (days < -60) return '';
    return days < 0 ? `(${Math.abs(days)} dni temu)` : `(za ${days} dni)`;
  }

  protected send(): void {
    this.sending.set(true);
    this.api.sendReminders().subscribe({
      next: (r) => {
        this.message.set(`Wysłano ${r.sent} e-maili. W wersji demonstracyjnej trafiają do logów API.`);
        this.sending.set(false);
      },
      error: (err) => {
        this.error.set(errorMessage(err));
        this.sending.set(false);
      },
    });
  }
}
