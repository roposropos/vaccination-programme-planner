using Microsoft.EntityFrameworkCore;
using VaxPlan.Api.Data;

namespace VaxPlan.Api.Services;

public record ReminderItem(
    int PatientId,
    string PatientName,
    string? GuardianName,
    string? Email,
    string? Phone,
    string VaccineCode,
    string VaccineName,
    int DoseNumber,
    DateOnly DueDate,
    DoseStatus Status,
    int DaysFromToday);

public class ReminderService(
    AppDbContext db,
    CalendarService calendar,
    INotificationSender sender,
    ILogger<ReminderService> logger)
{
    public async Task<List<ReminderItem>> GetPendingAsync(CancellationToken ct = default)
    {
        var vaccines = await db.LoadVaccineScheduleAsync(ct);
        var patients = await db.Patients.AsNoTracking().Include(p => p.Immunizations).ToListAsync(ct);
        var today = calendar.Today;

        return patients
            .SelectMany(p => calendar.BuildEntries(p, vaccines, p.Immunizations)
                .Where(e => e.Mandatory && e.Status is DoseStatus.Overdue or DoseStatus.DueSoon)
                .Select(e => new ReminderItem(
                    p.Id,
                    p.FullName,
                    p.GuardianName,
                    p.Email,
                    p.Phone,
                    e.VaccineCode,
                    e.VaccineName,
                    e.DoseNumber,
                    e.DueDate,
                    e.Status,
                    e.DueDate.DayNumber - today.DayNumber)))
            .OrderBy(r => r.Status == DoseStatus.Overdue ? 0 : 1)
            .ThenBy(r => r.DueDate)
            .ToList();
    }

    public async Task<int> SendRemindersAsync(CancellationToken ct = default)
    {
        var pending = await GetPendingAsync(ct);
        var sent = 0;

        foreach (var group in pending.Where(r => !string.IsNullOrWhiteSpace(r.Email)).GroupBy(r => r.PatientId))
        {
            var first = group.First();
            var lines = group.Select(r => r.Status == DoseStatus.Overdue
                ? $"- {r.VaccineName}, dawka {r.DoseNumber}: ZALEGŁA (termin {r.DueDate:dd.MM.yyyy})"
                : $"- {r.VaccineName}, dawka {r.DoseNumber}: termin {r.DueDate:dd.MM.yyyy}");

            var body =
                $"Dzień dobry,{Environment.NewLine}{Environment.NewLine}" +
                $"przypominamy o szczepieniach pacjenta {first.PatientName}:{Environment.NewLine}" +
                string.Join(Environment.NewLine, lines) +
                $"{Environment.NewLine}{Environment.NewLine}Prosimy o kontakt z przychodnią w celu umówienia wizyty.";

            await sender.SendAsync(first.Email!, $"Przypomnienie o szczepieniu – {first.PatientName}", body, ct);
            sent++;
        }

        logger.LogInformation("Wysłano {Count} przypomnień o szczepieniach.", sent);
        return sent;
    }
}
