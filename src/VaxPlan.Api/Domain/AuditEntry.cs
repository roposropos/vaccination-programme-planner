namespace VaxPlan.Api.Domain;

public class AuditEntry
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public int? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? Role { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string? IpAddress { get; set; }
}
