namespace VaxPlan.Api.Services;

public interface INotificationSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}

public class LoggingNotificationSender(ILogger<LoggingNotificationSender> logger) : INotificationSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        logger.LogInformation("[E-MAIL] Do: {To} | Temat: {Subject}{NewLine}{Body}", to, subject, Environment.NewLine, body);
        return Task.CompletedTask;
    }
}
