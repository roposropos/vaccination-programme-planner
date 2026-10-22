namespace VaxPlan.Api.Domain;

public class AppUser
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.Patient;
    public bool IsActive { get; set; } = true;

    public int? PatientId { get; set; }
    public Patient? Patient { get; set; }

    public DateTime CreatedAt { get; set; }
}
