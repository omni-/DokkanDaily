using DokkanDaily.Constants;
using DokkanDaily.Helpers;
using DokkanDaily.Models;
using DokkanDaily.Repository;
using DokkanDaily.Services.Interfaces;

namespace DokkanDaily.Services
{
    public class LeaderboardService(IDokkanDailyRepository repository) : ILeaderboardService
    {
        private readonly object _lock = new();
        private readonly Dictionary<int, List<LeaderboardUser>> _leaderboards = new();
        private readonly Dictionary<int, Task<List<LeaderboardUser>>> _inFlight = new();
        private readonly IDokkanDailyRepository _repository = repository;
        private readonly DateTime _season1Start = InternalConstants.Season1StartDate;

        public int GetCurrentSeason() => ((DateTime.UtcNow.Month - _season1Start.Month) + 12 * (DateTime.UtcNow.Year - _season1Start.Year)) + 1;

        public async Task<List<LeaderboardUser>> GetCurrentLeaderboard(bool force = false)
        {
            return await GetLeaderboardBySeason(GetCurrentSeason(), force);
        }

        public async Task<List<LeaderboardUser>> GetLeaderboardBySeason(int season, bool force = false)
        {
            if (!force)
            {
                Task<List<LeaderboardUser>> fetchTask;
                lock (_lock)
                {
                    if (_leaderboards.TryGetValue(season, out var cached) && cached.Count > 0)
                    {
                        return cached;
                    }

                    if (!_inFlight.TryGetValue(season, out fetchTask))
                    {
                        fetchTask = FetchAndCache(season);
                        _inFlight[season] = fetchTask;
                    }
                }

                return await fetchTask;
            }

            // Force path: bypass cache and deduplication
            var result = await FetchAndCache(season);
            return result;
        }

        private async Task<List<LeaderboardUser>> FetchAndCache(int season)
        {
            var dbResult = season == 0
                ? await _repository.GetHallOfFame()
                : await _repository.GetLeaderboardByDate(_season1Start.AddMonths(season - 1));

            var leaderboard = new List<LeaderboardUser>();

            foreach (var user in dbResult)
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

            lock (_lock)
            {
                _leaderboards[season] = leaderboard;
                _inFlight.Remove(season, out _);
            }

            return leaderboard;
        }
    }
}