using Dapper;
using DokkanDaily.Configuration;
using DokkanDaily.Helpers;
using DokkanDaily.Models.Database;
using DokkanDaily.Repository.Attributes;
using FastMember;
using Microsoft.Extensions.Options;
using System.Data;

namespace DokkanDaily.Repository
{
    public class DokkanDailyRepository : IDokkanDailyRepository
    {
        private ISqlConnectionWrapper SqlConnectionWrapper { get; }

        public DokkanDailyRepository(ISqlConnectionWrapper sqlConnectionWrapper, IOptions<DokkanDailySettings> settings)
        {
            SqlConnectionWrapper = sqlConnectionWrapper;
            SqlConnectionWrapper.ConnectionString = settings.Value.SqlServerConnectionString;
        }

        public async Task InsertDailyClears(IEnumerable<DbClear> clears)
        {
            try 
            {
                await SqlConnectionWrapper.OpenAsync();

                DynamicParameters dp = new();
                dp.Add("Clears", ToDataTable(clears).AsTableValuedParameter());
                dp.Add("ClearDate", DateTime.UtcNow);

                await SqlConnectionWrapper.ExecuteAsync(
                    "[Core].[ClearInsert]", dp);
            } 
            finally 
            { 
                SqlConnectionWrapper.Close();
            }
        }

        public async Task<IEnumerable<DbLeaderboardResult>> GetDailyLeaderboard()
        {
            try
            {
                await SqlConnectionWrapper.OpenAsync();

                List<DbLeaderboardResult> results = [];

                await foreach (var item in SqlConnectionWrapper.ExecuteAsync<DbLeaderboardResult>(
                    "[Core].[CurrentLeaderboardGet]", new()))
                {
                    results.Add(item);
                }

                return results;
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
    }
}
