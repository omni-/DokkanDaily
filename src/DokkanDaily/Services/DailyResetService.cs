using DokkanDaily.Constants;
using DokkanDaily.Helpers;
using DokkanDaily.Models.Database;
using DokkanDaily.Repository;

namespace DokkanDaily.Services;

public class DailyResetService(
    IAzureBlobService azureBlobService, 
    IDokkanDailyRepository repository,
    ILogger<DailyResetService> logger) : BackgroundService
{
    private readonly ILogger<DailyResetService> _logger = logger;
    private readonly IAzureBlobService _azureBlobService = azureBlobService;
    private readonly IDokkanDailyRepository _repository = repository;

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

            _logger.LogInformation("Starting daily reset...");
            try
            {
                //hacky, but better than having a random second reset
                Environment.SetEnvironmentVariable("DOTNET_DokkanDailySettings__SeedOffset", "0");

                // delete old clears
                await _azureBlobService.PruneContainers(30);

                // upload clears for the day
                var result = await _azureBlobService.GetFilesForTag(DDHelper.GetUtcNowDateTag());
                List<DbClear> clears = [];
                foreach(var clear in result)
                {
                    var props = await clear.GetPropertiesAsync(cancellationToken: stoppingToken);
                    var tags = props.Value.Metadata;

                    // skip upload in case of missing data
                    if (!tags.ContainsKey(DDConstants.USER_NAME_TAG) 
                        || !tags.ContainsKey(DDConstants.CLEAR_TIME_TAG) 
                        || !tags.ContainsKey(DDConstants.ITEMLESS_TAG))
                        continue;

                    clears.Add(new DbClear()
                    {
                        DokkanNickname = tags[DDConstants.USER_NAME_TAG],
                        ClearTime = tags[DDConstants.CLEAR_TIME_TAG],
                        ItemlessClear = bool.Parse(tags[DDConstants.ITEMLESS_TAG])
                    });
                    clears.MinBy(x =>
                    {
                        try
                        {
                            TimeSpan.TryParseExact(x.ClearTime, "h\\'mm\\\"ss\\.f", System.Globalization.CultureInfo.InvariantCulture, out TimeSpan result);
                            return result;
                        }
                        catch { return TimeSpan.MaxValue; }
                    }).IsDailyHighscore = true;

                    await _repository.InsertDailyClears(clears);
                }

                _logger.LogInformation("Reset complete.");
            }
            catch { }
        }
    }

    private async Task WaitUntilNextScheduledTime(CancellationToken ct)
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