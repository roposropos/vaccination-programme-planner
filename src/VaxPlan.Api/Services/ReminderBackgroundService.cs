using Microsoft.Extensions.Options;

namespace VaxPlan.Api.Services;

public class ReminderOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalHours { get; set; } = 24;
    public int InitialDelaySeconds { get; set; } = 30;
}

public class ReminderBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<ReminderOptions> options,
    ILogger<ReminderBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(settings.InitialDelaySeconds), stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromHours(Math.Max(1, settings.IntervalHours)));

            do
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var reminders = scope.ServiceProvider.GetRequiredService<ReminderService>();
                    await reminders.SendRemindersAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Nie udało się wysłać przypomnień.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // app is shutting down
        }
    }
}
