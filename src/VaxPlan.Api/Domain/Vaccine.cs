namespace VaxPlan.Api.Domain;

public class Vaccine
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string TargetDisease { get; set; } = string.Empty;

    public string CvxCode { get; set; } = string.Empty;

    public string Icd10Code { get; set; } = string.Empty;

    public bool Mandatory { get; set; } = true;

    public List<ScheduleDose> Doses { get; set; } = [];
}

public class ScheduleDose
{
    public int Id { get; set; }
    public int VaccineId { get; set; }
    public Vaccine? Vaccine { get; set; }

    public int DoseNumber { get; set; }

    public int AgeInMonths { get; set; }

    public string? Description { get; set; }
}
