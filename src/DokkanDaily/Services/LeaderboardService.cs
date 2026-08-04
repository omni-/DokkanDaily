using DokkanDaily.Constants;
using DokkanDaily.Helpers;
using DokkanDaily.Models;
using DokkanDaily.Models.Database;
using DokkanDaily.Repository;
using DokkanDaily.Services.Interfaces;
using System.Collections.Concurrent;

namespace DokkanDaily.Services
{
    public class LeaderboardService(IDokkanDailyRepository repository) : ILeaderboardService
    {
        // read and written concurrently by every page load and by the nightly reset
        private readonly ConcurrentDictionary<int, List<LeaderboardUser>> _leaderboards = [];
        private readonly IDokkanDailyRepository _repository = repository;
        private readonly DateTime _season1Start = InternalConstants.Season1StartDate;

        public int GetCurrentSeason()
        {
            DateTime now = DateTime.UtcNow;

            return ((now.Month - _season1Start.Month) + 12 * (now.Year - _season1Start.Year)) + 1;
        }

        public async Task<List<LeaderboardUser>> GetCurrentLeaderboard(bool force = false)
        {
            return await GetLeaderboardBySeason(GetCurrentSeason(), force);
        }

        public async Task<List<LeaderboardUser>> GetLeaderboardBySeason(int season, bool force = false)
        {
            if (force || !_leaderboards.TryGetValue(season, out List<LeaderboardUser> leaderboard) || leaderboard.Count == 0)
            {
                IEnumerable<DbLeaderboardResult> result = season == 0 ?
                    await _repository.GetHallOfFame()
                    : await _repository.GetLeaderboardByDate(_season1Start.AddMonths(season - 1));

                leaderboard = [];

                foreach (DbLeaderboardResult user in result)
                {
                    leaderboard.Add(new()
                    {
                        DiscordUsername = user.DiscordUsername,
                        DokkanNickname = user.DokkanNickname.UnescapeUnicode(),
                        DiscordId = user.DiscordId,
                        TotalHighscores = user.DailyHighscores,
                        ItemlessClears = user.ItemlessClears,
                        TotalScore = user.TotalClears + user.ItemlessClears + user.DailyHighscores
                    });
                }

                leaderboard = [.. leaderboard
                    .OrderByDescending(x => x.TotalScore)
                    .ThenByDescending(x => x.TotalHighscores)
                    .ThenByDescending(x => x.ItemlessClears)];
            }

            _leaderboards[season] = leaderboard;

            return leaderboard;
        }
    }
}
