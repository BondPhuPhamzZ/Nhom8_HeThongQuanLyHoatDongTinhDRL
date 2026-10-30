namespace StudentActivityManagement.API.Services;

/// <summary>Checks due session reminders at startup and once a minute.</summary>
public sealed class ReminderDispatchWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReminderDispatchWorker> _logger;

    public ReminderDispatchWorker(IServiceScopeFactory scopeFactory, ILogger<ReminderDispatchWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var count = await service.DispatchDueRemindersAsync(DateTime.UtcNow, stoppingToken);
                if (count > 0) _logger.LogInformation("Created {NotificationCount} activity reminders", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Activity reminder dispatch failed; will retry on the next interval");
            }
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        } while (true);
    }
}
