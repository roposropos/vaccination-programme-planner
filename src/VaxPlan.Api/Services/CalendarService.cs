using VaxPlan.Api.Domain;

namespace VaxPlan.Api.Services;

public enum DoseStatus
{
    Completed,

    Overdue,

    DueSoon,

    Planned
}

public record CalendarEntry(
    int VaccineId,
    string VaccineCode,
    string VaccineName,
    string TargetDisease,
    bool Mandatory,
    int DoseNumber,
    int SeriesDoses,
    int AgeInMonths,
    string? Description,
    DateOnly DueDate,
    DoseStatus Status,
    int? ImmunizationId,
    DateOnly? AdministeredOn,
    string? LotNumber);

public record PatientCalendar(
    int PatientId,
    string PatientName,
    DateOnly BirthDate,
    DateOnly Today,
    int Completed,
    int Overdue,
    int DueSoon,
    int Planned,
    IReadOnlyList<CalendarEntry> Entries);

// Works on plain objects (no DbContext), which keeps the core rules easy to unit test.
public class CalendarService(TimeProvider clock)
{
    public const int DueSoonWindowDays = 30;

    public DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    public IReadOnlyList<CalendarEntry> BuildEntries(
        Patient patient,
        IEnumerable<Vaccine> vaccines,
        IEnumerable<Immunization> immunizations)
    {
        var today = Today;
        var given = immunizations
            .Where(i => i.Status == ImmunizationStatus.Completed)
            .GroupBy(i => (i.VaccineId, i.DoseNumber))
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.AdministeredOn).First());

        var entries = new List<CalendarEntry>();
        foreach (var vaccine in vaccines)
        {
            var seriesDoses = vaccine.Doses.Count;
            foreach (var dose in vaccine.Doses.OrderBy(d => d.DoseNumber))
            {
                var dueDate = patient.BirthDate.AddMonths(dose.AgeInMonths);
                given.TryGetValue((vaccine.Id, dose.DoseNumber), out var immunization);

                var status = immunization is not null
                    ? DoseStatus.Completed
                    : ResolveStatus(dueDate, today);

                entries.Add(new CalendarEntry(
                    vaccine.Id,
                    vaccine.Code,
                    vaccine.Name,
                    vaccine.TargetDisease,
                    vaccine.Mandatory,
                    dose.DoseNumber,
                    seriesDoses,
                    dose.AgeInMonths,
                    dose.Description,
                    dueDate,
                    status,
                    immunization?.Id,
                    immunization?.AdministeredOn,
                    immunization?.LotNumber));
            }
        }

        return entries
            .OrderBy(e => e.DueDate)
            .ThenBy(e => e.VaccineCode)
            .ThenBy(e => e.DoseNumber)
            .ToList();
    }

    public PatientCalendar Build(Patient patient, IEnumerable<Vaccine> vaccines, IEnumerable<Immunization> immunizations)
    {
        var entries = BuildEntries(patient, vaccines, immunizations);
        return new PatientCalendar(
            patient.Id,
            patient.FullName,
            patient.BirthDate,
            Today,
            entries.Count(e => e.Status == DoseStatus.Completed),
            entries.Count(e => e.Status == DoseStatus.Overdue),
            entries.Count(e => e.Status == DoseStatus.DueSoon),
            entries.Count(e => e.Status == DoseStatus.Planned),
            entries);
    }

    public static DoseStatus ResolveStatus(DateOnly dueDate, DateOnly today)
    {
        if (dueDate < today)
        {
            return DoseStatus.Overdue;
        }

        return dueDate <= today.AddDays(DueSoonWindowDays) ? DoseStatus.DueSoon : DoseStatus.Planned;
    }
}
