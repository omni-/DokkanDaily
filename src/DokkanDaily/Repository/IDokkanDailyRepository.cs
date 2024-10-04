using DokkanDaily.Models.Database;

namespace DokkanDaily.Repository
{
    public interface IDokkanDailyRepository
    {
        Task InsertDailyClears(IEnumerable<DbClear> clears, DateTime dateOnly);

        Task<IEnumerable<DbLeaderboardResult>> GetDailyLeaderboard();
    }
}
