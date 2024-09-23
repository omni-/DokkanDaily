using DokkanDaily.Models;
using DokkanDaily.Repository;

namespace DokkanDaily.Services
{
    public class LeaderboardService(IDokkanDailyRepository repository) : ILeaderboardService
    {
        private List<LeaderboardUser> leaderboard = [];
        private readonly IDokkanDailyRepository _repository = repository;
        public async Task<List<LeaderboardUser>> GetDailyLeaderboard()
        {
            if (leaderboard.Count == 0)
            {
                var result = await _repository.GetDailyLeaderboard();

                foreach (var user in result)
                {
                    leaderboard.Add(new()
                    {
                        DiscordUsername = user.DiscordUsername,
                        DokkanNickname = user.DokkanNickname,
                        TotalScore = user.TotalClears + user.ItemlessClears + user.DailyHighscores
                    });
                }

                leaderboard = [.. leaderboard.OrderByDescending(x => x.TotalScore)];
            }

            return leaderboard;
        }
    }
}
