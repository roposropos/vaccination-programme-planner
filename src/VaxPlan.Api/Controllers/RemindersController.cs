using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VaxPlan.Api.Domain;
using VaxPlan.Api.Services;

namespace VaxPlan.Api.Controllers;

[ApiController]
[Route("api/reminders")]
[Authorize(Roles = Roles.Clinical)]
public class RemindersController(ReminderService reminders) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ReminderItem>>> List([FromQuery] DoseStatus? status, CancellationToken ct)
    {
        var items = await reminders.GetPendingAsync(ct);
        return status is null ? items : items.Where(i => i.Status == status).ToList();
    }

    /// <summary>Sends reminder e-mails now instead of waiting for the daily job.</summary>
    [HttpPost("send")]
    [Authorize(Roles = Roles.Nurse)]
    public async Task<IActionResult> Send(CancellationToken ct)
    {
        var sent = await reminders.SendRemindersAsync(ct);
        return Ok(new { sent });
    }
}
