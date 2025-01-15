using Dapper;
using DokkanDaily.Configuration;
using DokkanDaily.Helpers;
using DokkanDaily.Models;
using DokkanDaily.Models.Database;
using DokkanDaily.Models.Enums;
using DokkanDaily.Repository.Attributes;
using FastMember;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;

namespace DokkanDaily.Repository
{
    public class DokkanDailyRepository(ILogger<DokkanDailyRepository> logger, IOptions<DokkanDailySettings> settings) : IDokkanDailyRepository
    {
        private readonly ILogger<DokkanDailyRepository> _logger = logger;
        private readonly string _connectionString = settings.Value.SqlServerConnectionString;

        public async Task InsertDailyClears(IEnumerable<DbClear> clears, DateTime dateOnly)
        {
            using SqlConnection sqlConnection = new(_connectionString);

            _logger.LogInformation("Beginning daily clear insert...");

            await sqlConnection.OpenAsync();

            DynamicParameters dp = new();
            dp.Add("Clears", ToDataTable(clears).AsTableValuedParameter());
            dp.Add("ClearDate", dateOnly.Date);

            await sqlConnection.ExecuteAsync(
                "[Core].[ClearInsert]", dp);

            _logger.LogInformation("Daily clears inserted");
        }

        public async Task<IEnumerable<DbChallenge>> GetChallengeList(DateTime? cutoff)
        {
            using SqlConnection sqlConnection = new(_connectionString);

            _logger.LogInformation("Getting challenge list...");

            await sqlConnection.OpenAsync();

            DynamicParameters dp = new();
            if (cutoff != null) dp.Add("CutoffDateUTC", cutoff.Value);

            return await sqlConnection.QueryAsync<DbChallenge>(
                "[Core].[DailyChallengeListGet]", dp);
        }

        public async Task InsertChallenge(Challenge challenge)
        {
            using SqlConnection sqlConnection = new(_connectionString);

            _logger.LogInformation("Getting challenge list...");

            await sqlConnection.OpenAsync();

            List<DbLeaderboardResult> results = [];

            DynamicParameters dp = new();
            dp.Add("Event", challenge.TodaysEvent.Name);
            dp.Add("Stage", challenge.TodaysEvent.StageNumber);
            dp.Add("Date", DateTime.UtcNow.Date);
            dp.Add("DailyTypeName", challenge.DailyType.ToString());
            switch (challenge.DailyType)
            {
                case DailyType.Character:
                    dp.Add("LeaderFullName", challenge.Leader.FullName);
                    break;
                case DailyType.Category:
                    dp.Add("Category", challenge.Category.Name);
                    break;
                case DailyType.LinkSkill:
                    dp.Add("LinkSkill", challenge.LinkSkill.Name);
                    break;
            }

            await sqlConnection.ExecuteAsync(
                "[Core].[DailyInsert]", dp);
        }

        public async Task<IEnumerable<DbLeaderboardResult>> GetLeaderboardByDate(DateTime monthAndYear)
        {
            using SqlConnection sqlConnection = new(_connectionString);

            _logger.LogInformation("Getting current leaderboard...");

            await sqlConnection.OpenAsync();

            List<DbLeaderboardResult> results = [];

            DynamicParameters dp = new();
            dp.Add("MonthAndYear", monthAndYear);

            await foreach (var item in sqlConnection.QueryUnbufferedAsync<DbLeaderboardResult>(
                "[Core].[LeaderboardGetByDate]", dp))
            {
                results.Add(item);
            }

            _logger.LogInformation("Daily leaderboad retrieved");

            return results;
        }

        public async Task<IEnumerable<DbLeaderboardResult>> GetHallOfFame()
        {
            using SqlConnection sqlConnection = new(_connectionString);

            _logger.LogInformation("Getting Hall of Fame...");

            await sqlConnection.OpenAsync();

            List<DbLeaderboardResult> results = [];

            await foreach (var item in sqlConnection.QueryUnbufferedAsync<DbLeaderboardResult>(
                "[Core].[HallOfFameLeaderboardGet]", new()))
            {
                results.Add(item);
            }

            _logger.LogInformation("HoF retrieved");

            return results;
        }

        private DataTable ToDataTable<T>(IEnumerable<T> values) where T : class
        {
            Type type = typeof(T);
            TypeAccessor ta = TypeAccessor.Create(type);
            var orderedEnum = ta.GetMembers()
                .Where(x => x.GetMemberAttribute<DataTableIndex>() != null)
                .Select(x =>
                {
                    DataTableIndex dtiAttribute = x.GetMemberAttribute<DataTableIndex>();
                    return new { Member = x, ColumnIndex = dtiAttribute.Index };
                })
                .OrderBy(x => x.ColumnIndex);

            string[] cols = orderedEnum
                .Select(x => x.Member.Name)
                .ToArray();

            DataTable dt = new();
            using ObjectReader reader = ObjectReader.Create(values, cols);
            dt.Load(reader);

            return dt;
        }
    }
}
