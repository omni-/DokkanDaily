using DokkanDaily.Helpers;

namespace DokkanDaily.Services;

public class DailyResetService(IAzureBlobService azureBlobService, ILogger<DailyResetService> logger) : BackgroundService
{
    private readonly ILogger<DailyResetService> _logger = logger;
    private readonly IAzureBlobService _azureBlobService = azureBlobService;

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


            await _azureBlobService.DeleteByTagAsync(DateTime.UtcNow.GetTagName());

            _logger.LogInformation("Waiting until next scheduled time...");
            await WaitUntilNextScheduledTime(stoppingToken);

            // delete yesterdays clears
            _logger.LogInformation("Starting daily reset...");
            try
            {

                string tagName = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1)).GetTagName();

                _logger.LogInformation("Deleting tag {@Tag}", tagName);

                await _azureBlobService.DeleteByTagAsync(tagName);

                _logger.LogInformation("Reset complete.");
            }
            catch { }
        }
    }

    private async Task WaitUntilNextScheduledTime(CancellationToken ct)
    {
        var schedule = new TimeOnly[] { new(0, 0) }; 
        var currentDateTime = DateTime.UtcNow;
        var nextScheduledTime = schedule
            .Select(record => GetNextDateTime(currentDateTime, record))
            .Min();

        var waitTime = nextScheduledTime - currentDateTime;
        _logger.LogInformation("Will execute work at {NextTime} UTC time in {WaitTime}", nextScheduledTime, waitTime);
        await Task.Delay(waitTime, ct);
    }
}