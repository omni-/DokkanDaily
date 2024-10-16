﻿using DokkanDaily.Constants;
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
        ILogger<ResetService> logger) : IResetService
    {
        private readonly ILogger<ResetService> _logger = logger;
        private readonly IAzureBlobService _azureBlobService = azureBlobService;
        private readonly ILeaderboardService _leaderboardService = leaderboardService;
        private readonly IDokkanDailyRepository _repository = repository;
        private readonly IRngHelperService _rngHelperService = rngHelperService;

        public async Task DoReset(int daysAgo = 0)
        {
            _logger.LogInformation("Starting daily reset...");

            // Reset RNGService/seeding
            _rngHelperService.Reset();

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

            var dailyWinner = clears.MinBy(x => x.ClearTimeSpan);
            dailyWinner.IsDailyHighscore = true;

            _logger.LogInformation("Calculated {@Clear} to be the fastest today.", dailyWinner);

            clears = clears
                .GroupBy(x => x.DokkanNickname)
                .Select(group =>
                    group.FirstOrDefault(x => x.IsDailyHighscore)
                    ?? group.FirstOrDefault(x => x.ItemlessClear)
                    ?? group.MinBy(x => x.ClearTimeSpan))
                .ToList();

            await _repository.InsertDailyClears(clears, date);

            _logger.LogInformation("Daily clears inserted. Updating leaderboard...");

            // force reload leaderboard
            await _leaderboardService.GetDailyLeaderboard(true);

            _logger.LogInformation("Leaderboard updated.");

            _logger.LogInformation("Reset complete.");
        }

    }
}
