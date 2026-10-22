namespace VaxPlan.Api.Domain;

public enum Sex
{
    Female,
    Male
}

public class Patient
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public string Pesel { get; set; } = string.Empty;

    public DateOnly BirthDate { get; set; }
    public Sex Sex { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }

    public string? GuardianName { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<Immunization> Immunizations { get; set; } = [];

    public string FullName => $"{FirstName} {LastName}";
}
