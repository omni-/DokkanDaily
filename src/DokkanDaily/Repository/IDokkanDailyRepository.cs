using DokkanDaily.Models;
using DokkanDaily.Models.Database;

namespace DokkanDaily.Repository
{
    public interface IDokkanDailyRepository
    {
        Task InsertDailyClears(IEnumerable<DbClear> clears, DateTime dateOnly);

        Task InsertChallenge(Challenge challenge);

        Task<IEnumerable<DbChallenge>> GetChallengeList(DateTime cutoff);

        Task<IEnumerable<DbLeaderboardResult>> GetLeaderboardByDate(DateTime monthAndYear);

        Task<IEnumerable<DbLeaderboardResult>> GetHallOfFame();
    }
}
