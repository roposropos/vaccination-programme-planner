import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';
import { ROLE_LABELS } from './core/labels';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    @if (auth.user(); as user) {
      <div class="shell">
        <nav aria-label="Główna nawigacja">
          <a class="wordmark" routerLink="/">VaxPlan</a>

          <div class="links">
            @if (auth.hasRole('Nurse', 'Doctor')) {
              <a routerLink="/patients" routerLinkActive="active">Pacjenci</a>
              <a routerLink="/reminders" routerLinkActive="active">Przypomnienia</a>
              <a routerLink="/reports" routerLinkActive="active">Realizacja programu</a>
            }
            @if (auth.hasRole('Patient')) {
              <a routerLink="/my-calendar" routerLinkActive="active">Szczepienia dziecka</a>
            }
            @if (auth.hasRole('Admin')) {
              <a routerLink="/admin/users" routerLinkActive="active">Konta</a>
              <a routerLink="/admin/audit" routerLinkActive="active">Rejestr dostępu</a>
            }
            <a routerLink="/vaccines" routerLinkActive="active">Kalendarz szczepień</a>
          </div>

          <div class="who">
            <strong>{{ user.displayName }}</strong>
            <span>{{ roleLabels[user.role] }}</span>
            <button class="link" (click)="auth.logout()">Wyloguj</button>
          </div>
        </nav>
        <main><router-outlet /></main>
      </div>
    } @else {
      <main><router-outlet /></main>
    }
  `,
  styles: `
    .shell { display: grid; grid-template-columns: 220px 1fr; min-height: 100vh; }
    nav {
      position: sticky; top: 0; height: 100vh;
      display: flex; flex-direction: column; gap: 2rem;
      padding: 1.5rem 1.25rem; background: var(--ink); color: #dfe5e8;
    }
    .wordmark { color: #fff; font-weight: 700; font-size: 1.35rem; text-decoration: none; letter-spacing: -0.01em; }
    .links { display: flex; flex-direction: column; gap: .15rem; }
    .links a { color: #c9d2d6; text-decoration: none; padding: .45rem .6rem; border-radius: 4px; }
    .links a:hover { color: #fff; background: rgba(255, 255, 255, .06); }
    .links a.active { color: #fff; background: rgba(255, 255, 255, .12); font-weight: 700; }
    .who { margin-top: auto; display: flex; flex-direction: column; gap: .15rem; font-size: .9rem; }
    .who strong { color: #fff; }
    .who button { color: #c9d2d6; align-self: flex-start; margin-top: .5rem; }
    main { min-width: 0; }

    @media (max-width: 760px) {
      .shell { grid-template-columns: 1fr; }
      nav { position: static; height: auto; flex-direction: row; flex-wrap: wrap; align-items: center; gap: .75rem 1rem; padding: .75rem 1rem; }
      .links { flex-direction: row; flex-wrap: wrap; }
      .who { margin: 0 0 0 auto; flex-direction: row; align-items: baseline; gap: .5rem; }
      .who span { display: none; }
      .who button { margin: 0; }
    }
  `,
})
export class AppComponent {
  protected readonly auth = inject(AuthService);
  protected readonly roleLabels = ROLE_LABELS;
}
