import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../core/api.service';
import { ROLE_LABELS, errorMessage } from '../core/labels';
import { AuditEntry, PagedResult, Role } from '../core/models';

@Component({
  selector: 'app-admin-audit',
  imports: [FormsModule, DatePipe],
  template: `
    <div class="page">
      <header class="page-head">
        <div>
          <h1>Rejestr dostępu</h1>
          <p class="lead">Każde wywołanie API: kto, kiedy, do jakiego zasobu i z jakim wynikiem. Odmowy dostępu też tu trafiają.</p>
        </div>
      </header>

      <form class="row filters" (ngSubmit)="load(1)">
        <label>Użytkownik <input name="user" [(ngModel)]="user" placeholder="adres e-mail" /></label>
        <label>Zasób <input name="path" [(ngModel)]="path" placeholder="np. /api/patients/5" /></label>
        <button type="submit" class="quiet">Filtruj</button>
      </form>

      @if (error()) { <div class="message error" role="alert">{{ error() }}</div> }

      @if (result(); as r) {
        <div class="table-wrap">
          <table>
            <thead><tr><th>Czas</th><th>Użytkownik</th><th>Rola</th><th>Żądanie</th><th class="num">Wynik</th><th>Adres IP</th></tr></thead>
            <tbody>
              @for (a of r.items; track a.id) {
                <tr [class.denied]="a.statusCode >= 400">
                  <td class="small nowrap">{{ a.timestamp | date: 'dd.MM.yyyy HH:mm:ss' }}</td>
                  <td>{{ a.userEmail ?? 'niezalogowany' }}</td>
                  <td class="nowrap">{{ roleLabel(a.role) }}</td>
                  <td class="small">{{ a.method }} {{ a.path }}</td>
                  <td class="num">{{ a.statusCode }}</td>
                  <td class="small muted">{{ a.ipAddress }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <div class="row pager">
          <span class="muted small">{{ r.total }} wpisów</span>
          <span class="spacer"></span>
          <button class="quiet compact" [disabled]="r.page <= 1" (click)="load(r.page - 1)">Nowsze</button>
          <button class="quiet compact" [disabled]="r.page * r.pageSize >= r.total" (click)="load(r.page + 1)">Starsze</button>
        </div>
      }
    </div>
  `,
  styles: `
    .filters { margin-bottom: 1.25rem; align-items: flex-end; }
    tr.denied td { color: var(--overdue); }
    td.nowrap { white-space: nowrap; }
    .pager { margin-top: 1rem; }
  `,
})
export class AdminAuditComponent implements OnInit {
  private readonly api = inject(ApiService);
  protected user = '';
  protected path = '';
  protected readonly result = signal<PagedResult<AuditEntry> | null>(null);
  protected readonly error = signal<string | null>(null);

  protected roleLabel(role: string | null): string {
    return role && role in ROLE_LABELS ? ROLE_LABELS[role as Role] : '';
  }

  ngOnInit(): void {
    this.load(1);
  }

  protected load(page: number): void {
    this.api.audit(this.user, this.path, page).subscribe({
      next: (r) => this.result.set(r),
      error: (err) => this.error.set(errorMessage(err)),
    });
  }
}
