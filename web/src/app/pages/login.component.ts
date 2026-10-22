import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { errorMessage } from '../core/labels';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  template: `
    <div class="login">
      <section class="intro">
        <h1>VaxPlan</h1>
        <p class="lead">
          Kalendarze szczepień dzieci w przychodni POZ: co zostało podane, co jest zaległe
          i o czym trzeba przypomnieć rodzicom.
        </p>

        <h2>Konta demonstracyjne</h2>
        <p class="muted small">Wszystkie mają hasło <strong>Demo123!</strong>. Dane pacjentów są fikcyjne.</p>
        <table>
          <tbody>
            @for (a of demoAccounts; track a.email) {
              <tr>
                <td><strong>{{ a.label }}</strong><br /><span class="muted small">{{ a.does }}</span></td>
                <td class="num"><button class="quiet compact" type="button" (click)="useDemo(a.email)">Wejdź</button></td>
              </tr>
            }
          </tbody>
        </table>
      </section>

      <section class="form">
        <h2>Logowanie</h2>
        @if (error()) { <div class="message error" role="alert">{{ error() }}</div> }
        <form (ngSubmit)="submit()">
          <label>E-mail <input name="email" type="email" [(ngModel)]="email" required autocomplete="username" /></label>
          <label>Hasło <input name="password" type="password" [(ngModel)]="password" required autocomplete="current-password" /></label>
          <button type="submit" [disabled]="busy()">{{ busy() ? 'Logowanie…' : 'Zaloguj się' }}</button>
        </form>
        <p class="muted small note">Projekt studencki, Politechnika Wrocławska. Nie do użytku klinicznego.</p>
      </section>
    </div>
  `,
  styles: `
    .login { display: grid; grid-template-columns: minmax(0, 1.2fr) minmax(0, 1fr); min-height: 100vh; }
    .intro { padding: 4rem 3.5rem; max-width: 640px; }
    .intro h1 { font-size: 2.6rem; }
    .lead { font-size: 1.15rem; margin: .75rem 0 3rem; max-width: 46ch; }
    .intro h2 { font-size: 1.1rem; margin-bottom: .25rem; }
    .intro table { margin-top: 1rem; border-top: 1px solid var(--rule); background: transparent; }
    .intro td { background: transparent; padding: .7rem 0; }
    .form { background: var(--surface); border-left: 1px solid var(--rule); padding: 4rem 3rem; display: flex; flex-direction: column; justify-content: center; }
    .form h2 { margin-bottom: 1.25rem; }
    form { display: flex; flex-direction: column; gap: 1rem; max-width: 360px; }
    form button { align-self: flex-start; }
    .note { margin-top: 2.5rem; }
    @media (max-width: 860px) {
      .login { grid-template-columns: 1fr; }
      .intro, .form { padding: 2rem 1.25rem; }
      .form { border-left: 0; border-top: 1px solid var(--rule); }
    }
  `,
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected email = '';
  protected password = '';
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly demoAccounts = [
    { label: 'Pielęgniarka', does: 'rejestruje szczepienia i wysyła przypomnienia', email: 'pielegniarka@vaxplan.local' },
    { label: 'Lekarz', does: 'przegląda pacjentów i realizację programu', email: 'lekarz@vaxplan.local' },
    { label: 'Rodzic', does: 'widzi kalendarz szczepień swojego dziecka', email: 'pacjent@vaxplan.local' },
    { label: 'Administrator', does: 'zarządza kontami, bez dostępu do danych medycznych', email: 'admin@vaxplan.local' },
  ];

  protected useDemo(email: string): void {
    this.email = email;
    this.password = 'Demo123!';
    this.submit();
  }

  protected submit(): void {
    this.busy.set(true);
    this.error.set(null);
    this.auth.login(this.email, this.password).subscribe({
      next: () => this.router.navigateByUrl(this.auth.homeUrl()),
      error: (err) => {
        this.error.set(errorMessage(err));
        this.busy.set(false);
      },
    });
  }
}
