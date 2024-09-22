using DokkanDaily.Models;

namespace DokkanDaily.Services
{
    public interface ILeaderboardService
    {
        Task<List<LeaderboardUser>> GetDailyLeaderboard();
    }
}
