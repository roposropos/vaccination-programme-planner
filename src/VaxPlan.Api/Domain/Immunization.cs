namespace VaxPlan.Api.Domain;

public enum ImmunizationStatus
{
    Completed,

    EnteredInError
}

public class Immunization
{
    public int Id { get; set; }

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public int VaccineId { get; set; }
    public Vaccine? Vaccine { get; set; }

    public int DoseNumber { get; set; }
    public DateOnly AdministeredOn { get; set; }

    public string LotNumber { get; set; } = string.Empty;

    public string? Site { get; set; }

    public string? Notes { get; set; }

    public int? PerformedById { get; set; }
    public AppUser? PerformedBy { get; set; }

    public ImmunizationStatus Status { get; set; } = ImmunizationStatus.Completed;

    public DateTime RecordedAt { get; set; }
}
