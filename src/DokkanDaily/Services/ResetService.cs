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

            // Capture the effective day before any asynchronous work. The scheduled reset starts
            // at 23:59 UTC, and draining OCR can cross midnight; recalculating afterwards would
            // make us read the next day's bucket instead of the day being finalized.
            DateTime date = DateTime.UtcNow - TimeSpan.FromDays(daysAgo);

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
                    await _webhookClient.PostAsync(tomorrowsChallenge?.ToWebhookPayload());
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Exception during automated reset");
                }
            }

            // delete old clears - an admin rerun should never trigger deletions
            if (!isAdhoc)
                await _azureBlobService.PruneContainers(30);
            else
                _logger.LogInformation("Adhoc reset. Skipping the prune job.");

            // a clear accepted seconds before the deadline may still be queued for OCR, and a blob
            // without identity metadata gets skipped below - so let analysis drain before reading
            await _azureBlobService.WaitForPendingAnalysis(InternalConstants.PendingOcrDrainTimeout);

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

                    if (!tags.TryGetValue(AzureConstants.ITEMLESS_TAG, out string itemless) || !bool.TryParse(itemless, out bool itemlessClear))
                    {
                        _logger.LogWarning("`ITEMLESS` tag missing or unparseable. Defaulting to false.");
                        itemlessClear = false;
                    }

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
                        DiscordId = tags.TryGetValue(AzureConstants.DISCORD_ID, out string discordId) ? discordId : null,
                        ClearTime = clearTime,
                        ItemlessClear = itemlessClear,
                        ClearTimeSpan = timeSpan
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process clear");
                }
            }

            // A sole accepted clear wins by default even when OCR could not read its time. With
            // multiple entrants, however, an unparsed time cannot be ranked against another clear.
            DbClear dailyWinner = clears.Count == 1
                ? clears[0]
                : clears
                    .Where(x => x.ClearTimeSpan != TimeSpan.MaxValue)
                    .MinBy(x => x.ClearTimeSpan);
            if (dailyWinner is null)
                _logger.LogWarning("No valid clears were submitted today. There is no daily highscore to award.");
            else
            {
                dailyWinner.IsDailyHighscore = true;
                _logger.LogInformation("Calculated {@Clear} to be the fastest today.", dailyWinner);
            }

            try
            {
                clears = [.. clears
                    .GroupBy(GetIdentityKey)
                    .Select(group =>
                    {
                        DbClear best = group.FirstOrDefault(x => x.IsDailyHighscore)
                            ?? group.MinBy(x => x.ClearTimeSpan);

                        // only one row per user per day is stored, so carry the itemless point across
                        // the whole day's submissions rather than losing it to the faster run
                        best.ItemlessClear = group.Any(x => x.ItemlessClear);

                        return best;
                    })];

                await _repository.InsertDailyClears(clears, date);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception while inserting daily clears");
            }

            _logger.LogInformation("Done processing daily clears. Updating leaderboard...");

            // force reload leaderboard
            await _leaderboardService.GetCurrentLeaderboard(true);

            _logger.LogInformation("Leaderboard updated.");

            _logger.LogInformation("Reset completed with no critical errors.");
        }

        /// <summary>
        /// Groups a day's clears by the strongest identity available. Falling back through Discord id
        /// and username keeps clears whose nickname could not be read from all collapsing onto a
        /// single null key, which would silently discard every one of them but the first.
        /// </summary>
        private static string GetIdentityKey(DbClear clear)
        {
            if (!string.IsNullOrWhiteSpace(clear.DiscordId)) return $"discordid:{clear.DiscordId}";

            if (!string.IsNullOrWhiteSpace(clear.DiscordUsername)) return $"discord:{clear.DiscordUsername}";

            return $"nickname:{clear.DokkanNickname}";
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

                    payload += $"\t{i switch { 0 or 1 or 2 => _medalEmojiCharMap[i], _ => $" {i + 1}\\." }} {leaderboard[i].GetDisplayName(true)} - **{leaderboard[i].TotalScore}** points\r\n";
                }
                payload += $"\r\nSeason **{lastSeason + 1}** has officially begun. Good luck!\r\n";

                await _webhookClient.PostAsync(payload.AddDokkandleDbcRolePing());
            }
            else
                _logger.LogInformation("Not the first of the month. No work to do.");
        }
    }
}
