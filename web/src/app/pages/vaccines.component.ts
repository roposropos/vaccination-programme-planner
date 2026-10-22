import { Component, OnInit, inject, signal } from '@angular/core';
import { ApiService } from '../core/api.service';
import { errorMessage, formatAge } from '../core/labels';
import { Vaccine } from '../core/models';

@Component({
  selector: 'app-vaccines',
  template: `
    <div class="page">
      <header class="page-head">
        <div>
          <h1>Kalendarz szczepień</h1>
          <p class="lead">
            Uproszczony kalendarz oparty na Programie Szczepień Ochronnych. Służy do demonstracji
            i nie zastępuje aktualnego komunikatu Głównego Inspektora Sanitarnego.
          </p>
        </div>
      </header>
      @if (error()) { <div class="message error" role="alert">{{ error() }}</div> }

      <div class="table-wrap">
        <table>
          <thead><tr><th>Szczepionka</th><th>Chroni przed</th><th>Dawki</th><th>CVX</th><th>ICD-10</th><th>Rodzaj</th></tr></thead>
          <tbody>
            @for (v of vaccines(); track v.id) {
              <tr>
                <td><strong>{{ v.name }}</strong></td>
                <td>{{ v.targetDisease }}</td>
                <td class="small">
                  <ol class="doses">
                    @for (d of v.doses; track d.doseNumber) { <li>{{ d.description || age(d.ageInMonths) }}</li> }
                  </ol>
                </td>
                <td>{{ v.cvxCode }}</td>
                <td>{{ v.icd10Code }}</td>
                <td>{{ v.mandatory ? 'obowiązkowe' : 'zalecane' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `,
  styles: `.doses { margin: 0; padding-left: 1.1rem; }`,
})
export class VaccinesComponent implements OnInit {
  private readonly api = inject(ApiService);
  protected readonly vaccines = signal<Vaccine[]>([]);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.api.vaccines().subscribe({ next: (v) => this.vaccines.set(v), error: (e) => this.error.set(errorMessage(e)) });
  }

  protected age(months: number): string {
    return months === 0 ? 'po urodzeniu' : formatAge(months);
  }
}
