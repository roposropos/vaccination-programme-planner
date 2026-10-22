using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VaxPlan.Api.Data;
using VaxPlan.Api.Domain;
using VaxPlan.Api.Dtos;

namespace VaxPlan.Api.Controllers;

[ApiController]
[Route("api/vaccines")]
[Authorize]
public class VaccinesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<VaccineDto>>> List(CancellationToken ct)
    {
        var vaccines = await db.LoadVaccineScheduleAsync(ct);
        return vaccines.Select(VaccineDto.From).ToList();
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<VaccineDto>> Create(SaveVaccineRequest request, CancellationToken ct)
    {
        Validate(request);
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Vaccines.AnyAsync(v => v.Code == code, ct))
        {
            throw new DomainException($"Szczepionka o kodzie {code} już istnieje.");
        }

        var vaccine = new Vaccine();
        Apply(vaccine, request, code);
        db.Vaccines.Add(vaccine);
        await db.SaveChangesAsync(ct);
        return StatusCode(StatusCodes.Status201Created, VaccineDto.From(vaccine));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<VaccineDto>> Update(int id, SaveVaccineRequest request, CancellationToken ct)
    {
        Validate(request);
        var vaccine = await db.Vaccines.Include(v => v.Doses).FirstOrDefaultAsync(v => v.Id == id, ct);
        if (vaccine is null)
        {
            return NotFound();
        }

        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Vaccines.AnyAsync(v => v.Code == code && v.Id != id, ct))
        {
            throw new DomainException($"Szczepionka o kodzie {code} już istnieje.");
        }

        var givenDoseNumbers = await db.Immunizations
            .Where(i => i.VaccineId == id && i.Status == ImmunizationStatus.Completed)
            .Select(i => i.DoseNumber)
            .Distinct()
            .ToListAsync(ct);

        var missing = givenDoseNumbers.Except(request.Doses.Select(d => d.DoseNumber)).ToList();
        if (missing.Count > 0)
        {
            throw new DomainException($"Nie można usunąć dawek, które zostały już podane pacjentom: {string.Join(", ", missing)}.");
        }

        db.ScheduleDoses.RemoveRange(vaccine.Doses);
        Apply(vaccine, request, code);
        await db.SaveChangesAsync(ct);
        return VaccineDto.From(vaccine);
    }

    private static void Validate(SaveVaccineRequest request)
    {
        var numbers = request.Doses.Select(d => d.DoseNumber).OrderBy(n => n).ToList();
        if (!numbers.SequenceEqual(Enumerable.Range(1, numbers.Count)))
        {
            throw new DomainException("Dawki muszą być ponumerowane kolejno od 1.");
        }

        var ages = request.Doses.OrderBy(d => d.DoseNumber).Select(d => d.AgeInMonths).ToList();
        if (ages.Any(a => a < 0) || ages.Zip(ages.Skip(1)).Any(p => p.Second < p.First))
        {
            throw new DomainException("Wiek podania kolejnych dawek nie może maleć ani być ujemny.");
        }
    }

    private static void Apply(Vaccine vaccine, SaveVaccineRequest request, string code)
    {
        vaccine.Code = code;
        vaccine.Name = request.Name.Trim();
        vaccine.TargetDisease = request.TargetDisease.Trim();
        vaccine.CvxCode = request.CvxCode.Trim();
        vaccine.Icd10Code = request.Icd10Code.Trim().ToUpperInvariant();
        vaccine.Mandatory = request.Mandatory;
        vaccine.Doses = request.Doses
            .OrderBy(d => d.DoseNumber)
            .Select(d => new ScheduleDose { DoseNumber = d.DoseNumber, AgeInMonths = d.AgeInMonths, Description = d.Description })
            .ToList();
    }
}
