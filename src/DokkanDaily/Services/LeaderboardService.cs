using DokkanDaily.Constants;
using DokkanDaily.Helpers;
using DokkanDaily.Models;
using DokkanDaily.Repository;
using DokkanDaily.Services.Interfaces;

namespace DokkanDaily.Services
{
    public class LeaderboardService(IDokkanDailyRepository repository) : ILeaderboardService
    {
        private Dictionary<int, List<LeaderboardUser>> _leaderboards = [];
        private readonly IDokkanDailyRepository _repository = repository;
        private readonly DateTime _season1Start = InternalConstants.Season1StartDate;

        public int GetCurrentSeason() => ((DateTime.UtcNow.Month - _season1Start.Month) + 12 * (DateTime.UtcNow.Year - _season1Start.Year)) + 1;

        public async Task<List<LeaderboardUser>> GetDailyLeaderboard(bool force = false)
        {
            return await GetLeaderboardBySeason(GetCurrentSeason(), force);
        }

        public async Task<List<LeaderboardUser>> GetLeaderboardBySeason(int season, bool force = false)
        {
            if (force || !_leaderboards.TryGetValue(season, out var leaderboard) || leaderboard.Count == 0)
            {
                var result = season == 0 ? 
                    await _repository.GetHallOfFame() 
                    : await _repository.GetLeaderboardByDate(_season1Start.AddMonths(season - 1));

                leaderboard = [];

                foreach (var user in result)
                {
                    leaderboard.Add(new()
                    {
                        DiscordUsername = user.DiscordUsername,
                        DokkanNickname = DokkanDailyHelper.UnescapeUnicode(user.DokkanNickname),
                        TotalScore = user.TotalClears + user.ItemlessClears + user.DailyHighscores
                    });
                }

                leaderboard = [.. leaderboard.OrderByDescending(x => x.TotalScore)];
            }

            _leaderboards[season] = leaderboard;

            return leaderboard;
        }
    }
}
