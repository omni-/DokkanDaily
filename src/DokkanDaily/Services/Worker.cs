using DokkanDaily.Models.Enums;
using DokkanDaily.Services.Interfaces;

namespace DokkanDaily.Services;

public class Worker(
    IResetService resetService,
    ILogger<Worker> logger) : BackgroundService
{
    private readonly ILogger<Worker> _logger = logger;
    private readonly IResetService _resetService = resetService;

    private static readonly Dictionary<WorkType, TimeOnly> WorkSchedule = new()
    { 
        { WorkType.DailyReset, new(23, 59) },
        { WorkType.LeaderboardProcessing, new(1, 30) }
    };  

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
            WorkType taskToExecute = await WaitUntilNextScheduledTime(stoppingToken);

            try
            {
                if (taskToExecute == WorkType.DailyReset)
                    await _resetService.DoReset();
                else if (taskToExecute == WorkType.LeaderboardProcessing)
                    await _resetService.ProcessLeaderboard();
            }
            catch (Exception e) { _logger.LogError(e, "Unhandled exception while attempting to invoke reset service"); }
        }
    }

    protected virtual async Task<WorkType> WaitUntilNextScheduledTime(CancellationToken ct)
    {
        var currentDateTime = DateTime.UtcNow;
        var nextScheduledTime = WorkSchedule
            .Select(x => new { Task = x.Key, Time = GetNextDateTime(currentDateTime, x.Value) })
            .MinBy(record => record.Time);

        var waitTime = nextScheduledTime.Time - currentDateTime;
        _logger.LogInformation("Will execute work at {NextTime} UTC (in {WaitTime})", nextScheduledTime.Time.ToShortTimeString(), waitTime.ToString(@"hh\hmm\m"));
        await Task.Delay(waitTime, ct);

        return nextScheduledTime.Task;
    }
}