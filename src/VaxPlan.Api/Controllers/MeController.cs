using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VaxPlan.Api.Data;
using VaxPlan.Api.Domain;
using VaxPlan.Api.Dtos;
using VaxPlan.Api.Infrastructure;
using VaxPlan.Api.Services;

namespace VaxPlan.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize(Roles = Roles.Patient)]
public class MeController(AppDbContext db, CalendarService calendar) : ControllerBase
{
    [HttpGet("profile")]
    public async Task<ActionResult<PatientDto>> Profile(CancellationToken ct)
    {
        var patient = await LoadOwnPatientAsync(includeImmunizations: false, ct);
        if (patient is null)
        {
            return Forbid();
        }

        return PatientDto.From(patient);
    }

    [HttpGet("calendar")]
    public async Task<ActionResult<PatientCalendar>> Calendar(CancellationToken ct)
    {
        var patient = await LoadOwnPatientAsync(includeImmunizations: true, ct);
        if (patient is null)
        {
            return Forbid();
        }

        return calendar.Build(patient, await db.LoadVaccineScheduleAsync(ct), patient.Immunizations);
    }

    private async Task<Patient?> LoadOwnPatientAsync(bool includeImmunizations, CancellationToken ct)
    {
        // The record id comes from the token, never from the URL, so a parent cannot open another child.
        var patientId = User.GetPatientId();
        if (patientId is null)
        {
            return null;
        }

        IQueryable<Patient> query = db.Patients.AsNoTracking();
        if (includeImmunizations)
        {
            query = query.Include(p => p.Immunizations);
        }

        return await query.FirstOrDefaultAsync(p => p.Id == patientId, ct);
    }
}
