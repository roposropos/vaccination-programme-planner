import { DoseStatus, Role } from './models';

export const STATUS_LABELS: Record<DoseStatus, string> = {
  Completed: 'Podana',
  Overdue: 'Zaległa',
  DueSoon: 'Wkrótce',
  Planned: 'Zaplanowana',
};

export const ROLE_LABELS: Record<Role, string> = {
  Patient: 'Opiekun pacjenta',
  Nurse: 'Pielęgniarka',
  Doctor: 'Lekarz',
  Admin: 'Administrator',
};

function years(n: number): string {
  if (n === 1) return '1 rok';
  const last = n % 10;
  const teen = n % 100 >= 12 && n % 100 <= 14;
  return `${n} ${last >= 2 && last <= 4 && !teen ? 'lata' : 'lat'}`;
}

/** Child's age in plain Polish, e.g. "2 lata 3 mies.". */
export function formatAge(months: number): string {
  if (months < 12) return months === 0 ? 'poniżej miesiąca' : `${months} mies.`;
  const rest = months % 12;
  return rest === 0 ? years(Math.floor(months / 12)) : `${years(Math.floor(months / 12))} ${rest} mies.`;
}

/** Column heading in the schedule grid: "urodzenie", "2 mies.", "6 lat". */
export function scheduleAge(months: number): string {
  if (months === 0) return 'urodzenie';
  return months < 24 ? `${months} mies.` : years(Math.round(months / 12));
}

/** Today as yyyy-MM-dd in local time (value for <input type="date">). */
export function todayIso(): string {
  const d = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/** Readable message from a ProblemDetails or validation response. */
export function errorMessage(err: unknown): string {
  const e = err as { status?: number; error?: { title?: string; errors?: Record<string, string[]> } };
  if (e?.status === 0) return 'Nie można połączyć się z serwerem. Sprawdź, czy API jest uruchomione.';
  const errors = e?.error?.errors;
  if (errors) {
    const first = Object.values(errors)[0];
    if (first?.length) return first[0];
  }
  return e?.error?.title ?? 'Coś poszło nie tak. Spróbuj ponownie.';
}
