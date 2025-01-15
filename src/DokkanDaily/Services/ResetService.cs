using DokkanDaily.Constants;
using DokkanDaily.Helpers;
using DokkanDaily.Models.Database;
using DokkanDaily.Repository;
using DokkanDaily.Services.Interfaces;

namespace DokkanDaily.Services
{
    public class ResetService(IAzureBlobService azureBlobService,
        IDokkanDailyRepository repository,
        ILeaderboardService leaderboardService,
        IRngHelperService rngHelperService,
        DiscordWebhookClient webhookClient,
        ILogger<ResetService> logger) : IResetService
    {
        private readonly ILogger<ResetService> _logger = logger;
        private readonly IAzureBlobService _azureBlobService = azureBlobService;
        private readonly ILeaderboardService _leaderboardService = leaderboardService;
        private readonly IDokkanDailyRepository _repository = repository;
        private readonly IRngHelperService _rngHelperService = rngHelperService;
        private readonly DiscordWebhookClient _webhookClient = webhookClient;

        private static readonly Dictionary<int, string> _medalEmojiCharMap = new()
        {
            { 0, char.ConvertFromUtf32(0x1F947) }, // 🥇
            { 1, char.ConvertFromUtf32(0x1F948) }, // 🥈
            { 2, char.ConvertFromUtf32(0x1F949) }, // 🥉
        };

        public async Task DoReset(int daysAgo = 0, bool isAdhoc = false)
        {
            _logger.LogInformation("Starting daily reset...");

            // get old challenge
            var todaysChallenge = await _rngHelperService.GetDailyChallenge();

            if (!isAdhoc)
            {
                try
                {
                    // insert challenge
                    await _repository.InsertChallenge(todaysChallenge);

                    // delay to avoid weird deadlock? idk and i can't repro :(
                    await Task.Delay(5);

                    // Reset RNGService/seeding
                    var tomorrowsChallenge = await _rngHelperService.UpdateDailyChallenge();

                    // send webhook notfication
                    await _webhookClient.PostAsync(tomorrowsChallenge.ToWebhookPayload());
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Exception during automated reset");
                }
            }

            // delete old clears
            await _azureBlobService.PruneContainers(30);

            DateTime date = DateTime.UtcNow - TimeSpan.FromDays(daysAgo);

            // upload clears for the day
            string tag = date.GetTagFromDate();
            var result = await _azureBlobService.GetFilesForTag(tag, _azureBlobService.GetBucketNameForDate(tag));
            List<DbClear> clears = [];

            _logger.LogInformation("Processing daily clears...");
            foreach (var clear in result)
            {
                try
                {
                    var props = await clear.GetPropertiesAsync();
                    var tags = props.Value.Metadata;

                    // skip upload in case we don't know who the clear belongs to or it was marked invalid
                    if (tags.TryGetValue(AzureConstants.INVALID_TAG, out string invalid))
                    {
                        _ = bool.TryParse(invalid, out bool invalidResult);
                        if (invalidResult) continue;
                    }
                    if (!tags.TryGetValue(AzureConstants.USER_NAME_TAG, out _) && !tags.TryGetValue(AzureConstants.DISCORD_NAME_TAG, out _))
                    {
                        _logger.LogWarning("Failed to extract a username and user was not logged in. Skipping clear entirely.");
                        continue;
                    }

                    if (!tags.TryGetValue(AzureConstants.ITEMLESS_TAG, out string itemless))
                    {
                        _logger.LogWarning("`ITEMLESS` tag missing. Defaulting to false.");
                    }
                ;
                    if (!tags.TryGetValue(AzureConstants.CLEAR_TIME_TAG, out string clearTime) || !DokkanDailyHelper.TryParseDokkanTimeSpan(clearTime, out TimeSpan timeSpan))
                    {
                        _logger.LogWarning("`CLEARTIME` tag missing. Defaulting to TimeSpan.MaxValue.");
                        timeSpan = TimeSpan.MaxValue;
                        clearTime = "99'99\"99.9";
                    }
                    clears.Add(new DbClear()
                    {
                        DokkanNickname = tags.TryGetValue(AzureConstants.USER_NAME_TAG, out string nickname) ? nickname : null,
                        DiscordUsername = tags.TryGetValue(AzureConstants.DISCORD_NAME_TAG, out string discord) ? discord : null,
                        ClearTime = clearTime,
                        ItemlessClear = bool.Parse(itemless),
                        ClearTimeSpan = timeSpan
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process clear");
                }
            }

            var dailyWinner = clears.MinBy(x => x.ClearTimeSpan);
            dailyWinner.IsDailyHighscore = true;

            _logger.LogInformation("Calculated {@Clear} to be the fastest today.", dailyWinner);

            try
            {
                clears = clears
                    .GroupBy(x => x.DokkanNickname)
                    .Select(group =>
                        group.FirstOrDefault(x => x.IsDailyHighscore)
                        ?? group.FirstOrDefault(x => x.ItemlessClear)
                        ?? group.MinBy(x => x.ClearTimeSpan))
                    .ToList();

                await _repository.InsertDailyClears(clears, date);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception while inserting daily clears");
            }

            _logger.LogInformation("Updating leaderboard...");

            // force reload leaderboard
            await _leaderboardService.GetCurrentLeaderboard(true);

            _logger.LogInformation("Leaderboard updated.");

            _logger.LogInformation("Reset completed with no fatal errors.");
        }

        public async Task ProcessLeaderboard()
        {
            if (DateTime.UtcNow.Day == 1)
            {
                _logger.LogInformation("Sending end of season summary to #daily-challenge...");
                int lastSeason = _leaderboardService.GetCurrentSeason() - 1;
                var leaderboard = (await _leaderboardService.GetLeaderboardBySeason(lastSeason)).ToList();
                string payload = $"# Season finish!\r\nSeason **{lastSeason}** has come to a close. Thank you to everyone who participated! Please congratulate the top scorers this season:\r\n\r\n";
                for (int i = 0; i < 5; i++)
                {
                    if (i >= leaderboard.Count) break;

                    payload += $"\t{i switch { 0 or 1 or 2 => _medalEmojiCharMap[i], _ => $" {i + 1}\\." }} {leaderboard[i].GetDisplayName()} - **{leaderboard[i].TotalScore}** points\r\n";
                }
                payload += $"\r\nSeason **{lastSeason + 1}** has officially begun. Good luck!\r\n";

                await _webhookClient.PostAsync(payload.AddDokkandleDbcRolePing());
            }
            else
                _logger.LogInformation("Not the first of the month. No work to do.");
        }
    }
}
