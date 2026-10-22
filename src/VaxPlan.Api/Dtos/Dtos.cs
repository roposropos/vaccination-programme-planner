using System.ComponentModel.DataAnnotations;
using VaxPlan.Api.Domain;
using VaxPlan.Api.Services;

namespace VaxPlan.Api.Dtos;

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public record LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public record UserDto(int Id, string Email, string DisplayName, string Role, int? PatientId, bool IsActive)
{
    public static UserDto From(AppUser u) => new(u.Id, u.Email, u.DisplayName, u.Role, u.PatientId, u.IsActive);
}

public record LoginResponse(string Token, DateTime ExpiresAt, UserDto User);

public record CreateUserRequest
{
    [Required, EmailAddress, StringLength(200)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(200)]
    public string DisplayName { get; init; } = string.Empty;

    [Required]
    public string Role { get; init; } = Roles.Nurse;

    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;

    public int? PatientId { get; init; }
}

public record SetUserActiveRequest(bool IsActive);

public record PatientListItem(
    int Id,
    string FirstName,
    string LastName,
    string Pesel,
    DateOnly BirthDate,
    int AgeInMonths,
    int CompletedCount,
    int OverdueCount,
    int DueSoonCount);

public record PatientDto(
    int Id,
    string FirstName,
    string LastName,
    string Pesel,
    DateOnly BirthDate,
    Sex Sex,
    string? Email,
    string? Phone,
    string? GuardianName)
{
    public static PatientDto From(Patient p) =>
        new(p.Id, p.FirstName, p.LastName, p.Pesel, p.BirthDate, p.Sex, p.Email, p.Phone, p.GuardianName);
}

public record CreatePatientRequest
{
    [Required, StringLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required, StringLength(11, MinimumLength = 11)]
    public string Pesel { get; init; } = string.Empty;

    [EmailAddress, StringLength(200)]
    public string? Email { get; init; }

    [StringLength(30)]
    public string? Phone { get; init; }

    [StringLength(200)]
    public string? GuardianName { get; init; }
}

public record UpdatePatientContactRequest
{
    [EmailAddress, StringLength(200)]
    public string? Email { get; init; }

    [StringLength(30)]
    public string? Phone { get; init; }

    [StringLength(200)]
    public string? GuardianName { get; init; }
}

public record ImmunizationDto(
    int Id,
    int PatientId,
    int VaccineId,
    string VaccineCode,
    string VaccineName,
    int DoseNumber,
    DateOnly AdministeredOn,
    string LotNumber,
    string? Site,
    string? Notes,
    string? PerformedBy,
    ImmunizationStatus Status,
    DateTime RecordedAt)
{
    public static ImmunizationDto From(Immunization i) => new(
        i.Id,
        i.PatientId,
        i.VaccineId,
        i.Vaccine?.Code ?? string.Empty,
        i.Vaccine?.Name ?? string.Empty,
        i.DoseNumber,
        i.AdministeredOn,
        i.LotNumber,
        i.Site,
        i.Notes,
        i.PerformedBy?.DisplayName,
        i.Status,
        i.RecordedAt);
}

public record RegisterImmunizationRequest
{
    [Range(1, int.MaxValue)]
    public int VaccineId { get; init; }

    [Range(1, 10)]
    public int DoseNumber { get; init; }

    [Required]
    public DateOnly? AdministeredOn { get; init; }

    [Required, StringLength(50)]
    public string LotNumber { get; init; } = string.Empty;

    [StringLength(100)]
    public string? Site { get; init; }

    [StringLength(1000)]
    public string? Notes { get; init; }

    public RegisterImmunizationCommand ToCommand() =>
        new(VaccineId, DoseNumber, AdministeredOn!.Value, LotNumber, Site, Notes);
}

public record ScheduleDoseDto(int DoseNumber, int AgeInMonths, string? Description);

public record VaccineDto(
    int Id,
    string Code,
    string Name,
    string TargetDisease,
    string CvxCode,
    string Icd10Code,
    bool Mandatory,
    IReadOnlyList<ScheduleDoseDto> Doses)
{
    public static VaccineDto From(Vaccine v) => new(
        v.Id, v.Code, v.Name, v.TargetDisease, v.CvxCode, v.Icd10Code, v.Mandatory,
        v.Doses.OrderBy(d => d.DoseNumber).Select(d => new ScheduleDoseDto(d.DoseNumber, d.AgeInMonths, d.Description)).ToList());
}

public record SaveVaccineRequest
{
    [Required, StringLength(20)]
    public string Code { get; init; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(200)]
    public string TargetDisease { get; init; } = string.Empty;

    [Required, StringLength(10)]
    public string CvxCode { get; init; } = string.Empty;

    [Required, StringLength(10)]
    public string Icd10Code { get; init; } = string.Empty;

    public bool Mandatory { get; init; } = true;

    [Required, MinLength(1)]
    public List<ScheduleDoseDto> Doses { get; init; } = [];
}

public record AuditEntryDto(long Id, DateTime Timestamp, string? UserEmail, string? Role, string Method, string Path, int StatusCode, string? IpAddress)
{
    public static AuditEntryDto From(AuditEntry a) =>
        new(a.Id, a.Timestamp, a.UserEmail, a.Role, a.Method, a.Path, a.StatusCode, a.IpAddress);
}
