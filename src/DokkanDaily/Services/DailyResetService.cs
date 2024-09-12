public class DailyResetService : BackgroundService
{
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
            await WaitUntilNextScheduledTime(stoppingToken);

            // TODO: Daily Reset Activities!
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
        await Task.Delay(waitTime, ct);
    }
}