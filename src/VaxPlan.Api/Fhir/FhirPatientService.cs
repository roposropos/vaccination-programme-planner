using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using VaxPlan.Api.Data;
using VaxPlan.Api.Domain;
using VaxPlan.Api.Services;

namespace VaxPlan.Api.Fhir;

public class FhirPatientService(AppDbContext db, FhirMapper mapper, CalendarService calendar)
{
    public async Task<Patient> LoadPatientAsync(int patientId, CancellationToken ct = default) =>
        await db.Patients
            .AsNoTracking()
            .Include(p => p.Immunizations).ThenInclude(i => i.Vaccine).ThenInclude(v => v!.Doses)
            .Include(p => p.Immunizations).ThenInclude(i => i.PerformedBy)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == patientId, ct)
        ?? throw new NotFoundException($"Nie znaleziono pacjenta o id {patientId}.");

    public async Task<JsonObject> GetRecommendationAsync(Patient patient, CancellationToken ct = default)
    {
        var vaccines = await db.LoadVaccineScheduleAsync(ct);
        var patientCalendar = calendar.Build(patient, vaccines, patient.Immunizations);
        return mapper.ToRecommendation(patientCalendar, vaccines);
    }

    public async Task<List<JsonObject>> GetAllResourcesAsync(int patientId, CancellationToken ct = default)
    {
        var patient = await LoadPatientAsync(patientId, ct);
        var resources = new List<JsonObject> { mapper.ToPatient(patient) };

        resources.AddRange(patient.Immunizations
            .Where(i => i.PerformedBy is not null)
            .Select(i => i.PerformedBy!)
            .DistinctBy(u => u.Id)
            .Select(mapper.ToPractitioner));

        resources.AddRange(patient.Immunizations.OrderBy(i => i.AdministeredOn).Select(mapper.ToImmunization));
        resources.Add(await GetRecommendationAsync(patient, ct));
        return resources;
    }
}
