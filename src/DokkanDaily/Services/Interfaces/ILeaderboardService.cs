using DokkanDaily.Models;

namespace DokkanDaily.Services.Interfaces
{
    public interface ILeaderboardService
    {
        Task<List<LeaderboardUser>> GetLeaderboardBySeason(int season, bool force = false);

        Task<List<LeaderboardUser>> GetDailyLeaderboard(bool force = false);

        int GetCurrentSeason();
    }
}
