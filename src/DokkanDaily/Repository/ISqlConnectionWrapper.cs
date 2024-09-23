using Dapper;
using System.Data;
using System.Data.Common;

namespace DokkanDaily.Repository
{
    public interface ISqlConnectionWrapper : IDbConnection
    {
        Task OpenAsync();

        Task CloseAsync();

        Task<DbDataReader> ExecuteAsync(string command, DynamicParameters dp, CommandType cmdType = CommandType.StoredProcedure);

        IAsyncEnumerable<T> ExecuteAsync<T>(string command, DynamicParameters dp, CommandType cmdType = CommandType.StoredProcedure);
    }
}
