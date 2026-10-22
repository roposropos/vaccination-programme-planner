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
[Route("api/patients")]
[Authorize(Roles = Roles.Clinical)]
public class PatientsController(
    AppDbContext db,
    CalendarService calendar,
    ImmunizationService immunizations,
    TimeProvider clock) : ControllerBase
{
    /// <summary>Patients with counts of given, overdue and upcoming doses.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<PatientListItem>>> List(
        [FromQuery] string? search,
        [FromQuery] bool overdueOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<Patient> query = db.Patients.AsNoTracking().Include(p => p.Immunizations);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p =>
                p.LastName.ToLower().Contains(term)
                || p.FirstName.ToLower().Contains(term)
                || p.Pesel.Contains(term));
        }

        var patients = await query.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ToListAsync(ct);
        var vaccines = await db.LoadVaccineScheduleAsync(ct);
        var today = calendar.Today;

        // Statuses are computed in memory. Fine for a clinic of a few hundred patients.
        var items = patients
            .Select(p =>
            {
                var entries = calendar.BuildEntries(p, vaccines, p.Immunizations);
                return new PatientListItem(
                    p.Id,
                    p.FirstName,
                    p.LastName,
                    p.Pesel,
                    p.BirthDate,
                    AgeInMonths(p.BirthDate, today),
                    entries.Count(e => e.Status == DoseStatus.Completed),
                    entries.Count(e => e.Status == DoseStatus.Overdue),
                    entries.Count(e => e.Status == DoseStatus.DueSoon));
            })
            .Where(i => !overdueOnly || i.OverdueCount > 0)
            .ToList();

        var pageItems = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<PatientListItem>(pageItems, items.Count, page, pageSize);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PatientDto>> Get(int id, CancellationToken ct)
    {
        var patient = await db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (patient is null)
        {
            return NotFound();
        }

        return PatientDto.From(patient);
    }

    /// <summary>Creates a patient. Birth date and sex are read from the PESEL number.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Nurse)]
    public async Task<ActionResult<PatientDto>> Create(CreatePatientRequest request, CancellationToken ct)
    {
        var pesel = request.Pesel.Trim();
        if (!Pesel.IsValid(pesel) || !Pesel.TryGetBirthDate(pesel, out var birthDate))
        {
            throw new DomainException("Nieprawidłowy numer PESEL (błędna suma kontrolna lub data).");
        }

        if (birthDate > DateOnly.FromDateTime(clock.GetLocalNow().DateTime))
        {
            throw new DomainException("Data urodzenia z numeru PESEL jest z przyszłości.");
        }

        if (await db.Patients.AnyAsync(p => p.Pesel == pesel, ct))
        {
            throw new DomainException("Pacjent o tym numerze PESEL już istnieje.");
        }

        var patient = new Patient
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Pesel = pesel,
            BirthDate = birthDate,
            Sex = Pesel.GetSex(pesel),
            Email = Normalize(request.Email),
            Phone = Normalize(request.Phone),
            GuardianName = Normalize(request.GuardianName),
            CreatedAt = DateTime.UtcNow
        };

        db.Patients.Add(patient);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = patient.Id }, PatientDto.From(patient));
    }

    /// <summary>Updates contact details. Identity data comes from PESEL and does not change.</summary>
    [HttpPut("{id:int}/contact")]
    [Authorize(Roles = Roles.Nurse)]
    public async Task<ActionResult<PatientDto>> UpdateContact(int id, UpdatePatientContactRequest request, CancellationToken ct)
    {
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (patient is null)
        {
            return NotFound();
        }

        patient.Email = Normalize(request.Email);
        patient.Phone = Normalize(request.Phone);
        patient.GuardianName = Normalize(request.GuardianName);
        await db.SaveChangesAsync(ct);
        return PatientDto.From(patient);
    }

    /// <summary>The patient's schedule with the status of every dose.</summary>
    [HttpGet("{id:int}/calendar")]
    public async Task<ActionResult<PatientCalendar>> Calendar(int id, CancellationToken ct)
    {
        var patient = await db.Patients.AsNoTracking().Include(p => p.Immunizations).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (patient is null)
        {
            return NotFound();
        }

        return calendar.Build(patient, await db.LoadVaccineScheduleAsync(ct), patient.Immunizations);
    }

    /// <summary>Vaccination history, including entries marked as entered-in-error.</summary>
    [HttpGet("{id:int}/immunizations")]
    public async Task<ActionResult<List<ImmunizationDto>>> Immunizations(int id, CancellationToken ct)
    {
        if (!await db.Patients.AnyAsync(p => p.Id == id, ct))
        {
            return NotFound();
        }

        var list = await db.Immunizations
            .AsNoTracking()
            .Include(i => i.Vaccine)
            .Include(i => i.PerformedBy)
            .Where(i => i.PatientId == id)
            .OrderByDescending(i => i.AdministeredOn)
            .ToListAsync(ct);

        return list.Select(ImmunizationDto.From).ToList();
    }

    /// <summary>Records a given vaccination.</summary>
    [HttpPost("{id:int}/immunizations")]
    [Authorize(Roles = Roles.Nurse)]
    public async Task<ActionResult<ImmunizationDto>> RegisterImmunization(int id, RegisterImmunizationRequest request, CancellationToken ct)
    {
        var immunization = await immunizations.RegisterAsync(id, request.ToCommand(), User.GetUserId(), ct);
        return StatusCode(StatusCodes.Status201Created, ImmunizationDto.From(immunization));
    }

    internal static int AgeInMonths(DateOnly birthDate, DateOnly today)
    {
        var months = (today.Year - birthDate.Year) * 12 + today.Month - birthDate.Month;
        if (today.Day < birthDate.Day)
        {
            months--;
        }

        return Math.Max(0, months);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
