using Microsoft.EntityFrameworkCore;
using VaxPlan.Api.Data;
using VaxPlan.Api.Domain;

namespace VaxPlan.Api.Services;

public record RegisterImmunizationCommand(
    int VaccineId,
    int DoseNumber,
    DateOnly AdministeredOn,
    string LotNumber,
    string? Site,
    string? Notes);

public class ImmunizationService(AppDbContext db, CalendarService calendar)
{
    public async Task<Immunization> RegisterAsync(
        int patientId,
        RegisterImmunizationCommand command,
        int? performedById,
        CancellationToken ct = default)
    {
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, ct)
            ?? throw new NotFoundException($"Nie znaleziono pacjenta o id {patientId}.");

        var vaccine = await db.Vaccines.Include(v => v.Doses).FirstOrDefaultAsync(v => v.Id == command.VaccineId, ct)
            ?? throw new NotFoundException($"Nie znaleziono szczepionki o id {command.VaccineId}.");

        if (vaccine.Doses.All(d => d.DoseNumber != command.DoseNumber))
        {
            throw new DomainException($"Szczepionka „{vaccine.Name}” nie ma dawki nr {command.DoseNumber} w kalendarzu szczepień.");
        }

        if (string.IsNullOrWhiteSpace(command.LotNumber))
        {
            throw new DomainException("Numer serii szczepionki jest wymagany.");
        }

        if (command.AdministeredOn > calendar.Today)
        {
            throw new DomainException("Data podania nie może być datą z przyszłości.");
        }

        if (command.AdministeredOn < patient.BirthDate)
        {
            throw new DomainException("Data podania nie może być wcześniejsza niż data urodzenia pacjenta.");
        }

        var previousDoses = await db.Immunizations
            .Where(i => i.PatientId == patientId
                        && i.VaccineId == vaccine.Id
                        && i.Status == ImmunizationStatus.Completed)
            .ToListAsync(ct);

        if (previousDoses.Any(i => i.DoseNumber == command.DoseNumber))
        {
            throw new DomainException($"Dawka nr {command.DoseNumber} tej szczepionki jest już zarejestrowana.");
        }

        if (command.DoseNumber > 1)
        {
            var previous = previousDoses.FirstOrDefault(i => i.DoseNumber == command.DoseNumber - 1)
                ?? throw new DomainException($"Najpierw należy zarejestrować dawkę nr {command.DoseNumber - 1}.");

            if (command.AdministeredOn <= previous.AdministeredOn)
            {
                throw new DomainException("Data podania musi być późniejsza niż data poprzedniej dawki.");
            }
        }

        var immunization = new Immunization
        {
            PatientId = patient.Id,
            Patient = patient,
            VaccineId = vaccine.Id,
            Vaccine = vaccine,
            DoseNumber = command.DoseNumber,
            AdministeredOn = command.AdministeredOn,
            LotNumber = command.LotNumber.Trim(),
            Site = string.IsNullOrWhiteSpace(command.Site) ? null : command.Site.Trim(),
            Notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim(),
            PerformedById = performedById,
            RecordedAt = DateTime.UtcNow
        };

        db.Immunizations.Add(immunization);
        await db.SaveChangesAsync(ct);

        if (performedById is not null)
        {
            await db.Entry(immunization).Reference(i => i.PerformedBy).LoadAsync(ct);
        }

        return immunization;
    }

    // Medical records are not deleted; a wrong entry is flagged, as in FHIR.
    public async Task<Immunization> MarkEnteredInErrorAsync(int immunizationId, CancellationToken ct = default)
    {
        var immunization = await db.Immunizations
            .Include(i => i.Vaccine)
            .Include(i => i.PerformedBy)
            .FirstOrDefaultAsync(i => i.Id == immunizationId, ct)
            ?? throw new NotFoundException($"Nie znaleziono szczepienia o id {immunizationId}.");

        if (immunization.Status == ImmunizationStatus.EnteredInError)
        {
            throw new DomainException("Ten wpis jest już oznaczony jako błędny.");
        }

        var laterDoseExists = await db.Immunizations.AnyAsync(i =>
            i.PatientId == immunization.PatientId
            && i.VaccineId == immunization.VaccineId
            && i.DoseNumber > immunization.DoseNumber
            && i.Status == ImmunizationStatus.Completed, ct);

        if (laterDoseExists)
        {
            throw new DomainException("Nie można anulować dawki, po której zarejestrowano już kolejne dawki.");
        }

        immunization.Status = ImmunizationStatus.EnteredInError;
        await db.SaveChangesAsync(ct);
        return immunization;
    }
}
