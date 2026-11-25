namespace StudentActivityManagement.API.Services;

public sealed class ActivityLifecycleWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    TimeProvider time,
    ILogger<ActivityLifecycleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Lifecycle:WorkerEnabled", true)) return;

        var interval = TimeSpan.FromSeconds(Math.Max(10, configuration.GetValue("Lifecycle:IntervalSeconds", 60)));
        using var timer = new PeriodicTimer(interval, time);
        try
        {
            do
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IActivityLifecycleService>().RunOnceAsync(stoppingToken);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogError(exception, "Activity lifecycle check failed; will retry on the next interval");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
