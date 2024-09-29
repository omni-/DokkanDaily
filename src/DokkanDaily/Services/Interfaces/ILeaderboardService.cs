using DokkanDaily.Models;

namespace DokkanDaily.Services.Interfaces
{
    public interface ILeaderboardService
    {
        Task<List<LeaderboardUser>> GetDailyLeaderboard(bool force = false);
    }
}
