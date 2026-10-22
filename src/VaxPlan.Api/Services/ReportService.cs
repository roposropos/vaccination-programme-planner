using Microsoft.EntityFrameworkCore;
using VaxPlan.Api.Data;
using VaxPlan.Api.Domain;

namespace VaxPlan.Api.Services;

public record VaccineCoverage(
    string VaccineCode,
    string VaccineName,
    int DoseNumber,
    int Eligible,
    int Completed,
    double CoveragePercent);

public record ReportSummary(
    DateOnly GeneratedOn,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    int Patients,
    int PatientsUpToDate,
    int PatientsWithOverdue,
    int OverdueDoses,
    int DueSoonDoses,
    int ImmunizationsInPeriod,
    double OverallCoveragePercent,
    IReadOnlyList<VaccineCoverage> Coverage);

public class ReportService(AppDbContext db, CalendarService calendar)
{
    public async Task<ReportSummary> GetSummaryAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var periodTo = to ?? calendar.Today;
        var periodFrom = from ?? periodTo.AddMonths(-12);
        if (periodFrom > periodTo)
        {
            throw new DomainException("Data początkowa okresu nie może być późniejsza niż końcowa.");
        }

        var vaccines = await db.LoadVaccineScheduleAsync(ct);
        var patients = await db.Patients.AsNoTracking().Include(p => p.Immunizations).ToListAsync(ct);
        return BuildSummary(patients, vaccines, periodFrom, periodTo);
    }

    public ReportSummary BuildSummary(
        IReadOnlyCollection<Patient> patients,
        IReadOnlyCollection<Vaccine> vaccines,
        DateOnly periodFrom,
        DateOnly periodTo)
    {
        var calendars = patients
            .Select(p => calendar.BuildEntries(p, vaccines, p.Immunizations))
            .ToList();

        var allEntries = calendars.SelectMany(c => c).ToList();

        // A dose counts once it is due or already given; future doses would drag coverage down.
        var coverage = allEntries
            .Where(e => e.Status is DoseStatus.Completed or DoseStatus.Overdue)
            .GroupBy(e => (e.VaccineCode, e.VaccineName, e.DoseNumber))
            .Select(g =>
            {
                var eligible = g.Count();
                var completed = g.Count(e => e.Status == DoseStatus.Completed);
                return new VaccineCoverage(g.Key.VaccineCode, g.Key.VaccineName, g.Key.DoseNumber, eligible, completed, Percent(completed, eligible));
            })
            .OrderBy(c => c.VaccineCode)
            .ThenBy(c => c.DoseNumber)
            .ToList();

        var eligibleTotal = coverage.Sum(c => c.Eligible);
        var completedTotal = coverage.Sum(c => c.Completed);

        var immunizationsInPeriod = patients
            .SelectMany(p => p.Immunizations)
            .Count(i => i.Status == ImmunizationStatus.Completed
                        && i.AdministeredOn >= periodFrom
                        && i.AdministeredOn <= periodTo);

        return new ReportSummary(
            calendar.Today,
            periodFrom,
            periodTo,
            patients.Count,
            calendars.Count(c => c.All(e => e.Status != DoseStatus.Overdue)),
            calendars.Count(c => c.Any(e => e.Status == DoseStatus.Overdue)),
            allEntries.Count(e => e.Status == DoseStatus.Overdue),
            allEntries.Count(e => e.Status == DoseStatus.DueSoon),
            immunizationsInPeriod,
            Percent(completedTotal, eligibleTotal),
            coverage);
    }

    private static double Percent(int part, int total) =>
        total == 0 ? 0 : Math.Round(100.0 * part / total, 1);
}
