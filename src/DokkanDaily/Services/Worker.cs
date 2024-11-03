using DokkanDaily.Services.Interfaces;

namespace DokkanDaily.Services;

public class Worker(
    IResetService resetService,
    ILogger<Worker> logger) : BackgroundService
{
    private readonly ILogger<Worker> _logger = logger;
    private readonly IResetService _resetService = resetService;

    private static DateTime GetNextDateTime(DateTime currentDateTime, TimeOnly time)
    {
        var scheduledTime = currentDateTime.Date + time.ToTimeSpan();

        if (currentDateTime >= scheduledTime)
            scheduledTime += TimeSpan.FromDays(1);

        return scheduledTime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Waiting until next scheduled time...");
            await WaitUntilNextScheduledTime(stoppingToken);

            try
            {
                await _resetService.DoReset();
            }
            catch(Exception e) { _logger.LogError(e, "Unhandled exception while attempting to invoke reset service"); }
        }
    }

    protected virtual async Task WaitUntilNextScheduledTime(CancellationToken ct)
    {
        var schedule = new TimeOnly[] { new(23, 59) };
        var currentDateTime = DateTime.UtcNow;
        var nextScheduledTime = schedule
            .Select(record => GetNextDateTime(currentDateTime, record))
            .Min();

        var waitTime = nextScheduledTime - currentDateTime;
        _logger.LogInformation("Will execute work at {NextTime} UTC time in {WaitTime}", nextScheduledTime, waitTime);
        await Task.Delay(waitTime, ct);
    }
}