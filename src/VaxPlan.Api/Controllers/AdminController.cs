using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VaxPlan.Api.Data;
using VaxPlan.Api.Domain;
using VaxPlan.Api.Dtos;
using VaxPlan.Api.Infrastructure;

namespace VaxPlan.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = Roles.Admin)]
public class AdminController(AppDbContext db, IPasswordHasher<AppUser> passwordHasher) : ControllerBase
{
    [HttpGet("users")]
    public async Task<ActionResult<List<UserDto>>> Users(CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking().OrderBy(u => u.Role).ThenBy(u => u.Email).ToListAsync(ct);
        return users.Select(UserDto.From).ToList();
    }

    [HttpPost("users")]
    public async Task<ActionResult<UserDto>> CreateUser(CreateUserRequest request, CancellationToken ct)
    {
        if (!Roles.All.Contains(request.Role))
        {
            throw new DomainException($"Nieznana rola: {request.Role}.");
        }

        if (request.Role == Roles.Patient)
        {
            if (request.PatientId is null || !await db.Patients.AnyAsync(p => p.Id == request.PatientId, ct))
            {
                throw new DomainException("Konto pacjenta musi być powiązane z istniejącą kartoteką pacjenta.");
            }
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            throw new DomainException("Użytkownik o tym adresie e-mail już istnieje.");
        }

        var user = new AppUser
        {
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            Role = request.Role,
            PatientId = request.Role == Roles.Patient ? request.PatientId : null,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return StatusCode(StatusCodes.Status201Created, UserDto.From(user));
    }

    [HttpPut("users/{id:int}/active")]
    public async Task<ActionResult<UserDto>> SetActive(int id, SetUserActiveRequest request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return NotFound();
        }

        if (user.Id == User.GetUserId() && !request.IsActive)
        {
            throw new DomainException("Nie można dezaktywować własnego konta.");
        }

        user.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return UserDto.From(user);
    }

    /// <summary>Access log, newest first, filtered by user or path.</summary>
    [HttpGet("audit")]
    public async Task<ActionResult<PagedResult<AuditEntryDto>>> Audit(
        [FromQuery] string? user,
        [FromQuery] string? path,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.AuditEntries.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(user))
        {
            var term = user.Trim().ToLower();
            query = query.Where(a => a.UserEmail != null && a.UserEmail.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(path))
        {
            var term = path.Trim().ToLower();
            query = query.Where(a => a.Path.ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<AuditEntryDto>(items.Select(AuditEntryDto.From).ToList(), total, page, pageSize);
    }
}
