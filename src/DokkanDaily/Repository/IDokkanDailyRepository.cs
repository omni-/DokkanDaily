using DokkanDaily.Models;
using DokkanDaily.Models.Database;

namespace DokkanDaily.Repository
{
    public interface IDokkanDailyRepository
    {
        Task InsertDailyClears(IEnumerable<DbClear> clears, DateTime dateOnly);

        Task<IEnumerable<DbChallenge>> GetChallengeList(DateTime cutoff);

        Task<IEnumerable<DbLeaderboardResult>> GetDailyLeaderboard();
    }
}
