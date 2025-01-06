using Dapper;
using DokkanDaily.Configuration;
using DokkanDaily.Helpers;
using DokkanDaily.Models;
using DokkanDaily.Models.Database;
using DokkanDaily.Models.Enums;
using DokkanDaily.Repository.Attributes;
using FastMember;
using Microsoft.Extensions.Options;
using System.Data;

namespace DokkanDaily.Repository
{
    public class DokkanDailyRepository : IDokkanDailyRepository
    {
        private ISqlConnectionWrapper SqlConnectionWrapper { get; }
        private readonly ILogger<DokkanDailyRepository> _logger;

        public DokkanDailyRepository(ISqlConnectionWrapper sqlConnectionWrapper, ILogger<DokkanDailyRepository> logger, IOptions<DokkanDailySettings> settings)
        {
            SqlConnectionWrapper = sqlConnectionWrapper;
            SqlConnectionWrapper.ConnectionString = settings.Value.SqlServerConnectionString;
            _logger = logger;
        }

        public async Task InsertDailyClears(IEnumerable<DbClear> clears, DateTime dateOnly)
        {
            _logger.LogInformation("Beginning daily clear insert...");

            try
            {
                await SqlConnectionWrapper.OpenAsync();

                DynamicParameters dp = new();
                dp.Add("Clears", ToDataTable(clears).AsTableValuedParameter());
                dp.Add("ClearDate", dateOnly.Date);

                await SqlConnectionWrapper.ExecuteAsync(
                    "[Core].[ClearInsert]", dp);
            }
            finally
            {
                SqlConnectionWrapper.Close();
            }

            _logger.LogInformation("Daily clears inserted");
        }

        public async Task<IEnumerable<DbChallenge>> GetChallengeList(DateTime cutoff)
        {
            _logger.LogInformation("Getting challenge list...");
            try
            {
                await SqlConnectionWrapper.OpenAsync();

                List<DbLeaderboardResult> results = [];

                DynamicParameters dp = new();
                dp.Add("CutoffDateUTC", cutoff);

                return await SqlConnectionWrapper.QueryAsync<DbChallenge>(
                    "[Core].[DailyChallengeListGet]", dp);
            }
            finally
            {
                SqlConnectionWrapper.Close();
            }
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

        public async Task InsertChallenge(Challenge challenge)
        {
            _logger.LogInformation("Getting challenge list...");
            try
            {
                await SqlConnectionWrapper.OpenAsync();

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

                await SqlConnectionWrapper.ExecuteAsync(
                    "[Core].[DailyInsert]", dp);
            }
            finally
            {
                SqlConnectionWrapper.Close();
            }
        }

        public async Task<IEnumerable<DbLeaderboardResult>> GetLeaderboardByDate(DateTime monthAndYear)
        {
            _logger.LogInformation("Getting current leaderboard...");
            try
            {
                await SqlConnectionWrapper.OpenAsync();

                List<DbLeaderboardResult> results = [];

                DynamicParameters dp = new();
                dp.Add("MonthAndYear", monthAndYear);

                await foreach (var item in SqlConnectionWrapper.ExecuteAsync<DbLeaderboardResult>(
                    "[Core].[LeaderboardGetByDate]", dp))
                {
                    results.Add(item);
                }

                _logger.LogInformation("Daily leaderboad retrieved");

                return results;
            }
            finally
            {
                SqlConnectionWrapper.Close();
            }
        }

        public async Task<IEnumerable<DbLeaderboardResult>> GetHallOfFame()
        {
            _logger.LogInformation("Getting Hall of Fame...");
            try
            {
                await SqlConnectionWrapper.OpenAsync();

                List<DbLeaderboardResult> results = [];

                await foreach (var item in SqlConnectionWrapper.ExecuteAsync<DbLeaderboardResult>(
                    "[Core].[HallOfFameLeaderboardGet]", new()))
                {
                    results.Add(item);
                }

                _logger.LogInformation("HoF retrieved");

                return results;
            }
            finally
            {
                SqlConnectionWrapper.Close();
            }
        }
    }
}
