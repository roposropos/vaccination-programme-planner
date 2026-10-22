using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VaxPlan.Api.Domain;
using VaxPlan.Api.Services;

namespace VaxPlan.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = Roles.Clinical)]
public class ReportsController(ReportService reports) : ControllerBase
{
    /// <summary>Coverage per dose, overdue counts and vaccinations given in the period.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<ReportSummary>> Summary([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        await reports.GetSummaryAsync(from, to, ct);
}
