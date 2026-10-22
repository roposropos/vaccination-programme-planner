using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VaxPlan.Api.Domain;
using VaxPlan.Api.Dtos;
using VaxPlan.Api.Services;

namespace VaxPlan.Api.Controllers;

[ApiController]
[Route("api/immunizations")]
[Authorize(Roles = Roles.Nurse)]
public class ImmunizationsController(ImmunizationService immunizations) : ControllerBase
{
    /// <summary>Marks a vaccination as entered-in-error. Records are never deleted.</summary>
    [HttpPost("{id:int}/entered-in-error")]
    public async Task<ActionResult<ImmunizationDto>> MarkEnteredInError(int id, CancellationToken ct)
    {
        var immunization = await immunizations.MarkEnteredInErrorAsync(id, ct);
        return ImmunizationDto.From(immunization);
    }
}
