using DokkanDaily.Models.Database;

namespace DokkanDaily.Repository
{
    public interface IDokkanDailyRepository
    {
        Task InsertDailyClears(IEnumerable<DbClear> clears);

        Task<IEnumerable<DbLeaderboardResult>> GetDailyLeaderboard();
    }
}
