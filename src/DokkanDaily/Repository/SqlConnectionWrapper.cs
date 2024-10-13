using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;

namespace DokkanDaily.Repository
{
    public class SqlConnectionWrapper(SqlConnection sqlConnection) : ISqlConnectionWrapper
    {
        public SqlConnectionWrapper() : this(new SqlConnection()) { }

        public string ConnectionString
        {
            get => sqlConnection.ConnectionString;
            set => sqlConnection.ConnectionString = value;
        }

        public int ConnectionTimeout => sqlConnection.ConnectionTimeout;

        public string Database => sqlConnection.Database;

        public ConnectionState State => sqlConnection.State;

        public IDbTransaction BeginTransaction()
        {
            return sqlConnection.BeginTransaction();
        }

        public IDbTransaction BeginTransaction(IsolationLevel il)
        {
            return (sqlConnection.BeginTransaction(il));
        }

        public void ChangeDatabase(string databaseName)
        {
            sqlConnection.ChangeDatabase(databaseName);
        }

        public void Close()
        {
            sqlConnection.Close();
        }

        public async Task CloseAsync()
        {
            await sqlConnection.CloseAsync();
        }

        public IDbCommand CreateCommand()
        {
            return sqlConnection.CreateCommand();
        }

        public void Dispose()
        {
            sqlConnection.Dispose();
        }

        public async Task<DbDataReader> ExecuteAsync(string command, DynamicParameters dp, CommandType cmdType = CommandType.StoredProcedure)
        {
            return await sqlConnection.ExecuteReaderAsync(command, dp, commandType: cmdType);
        }

        public IAsyncEnumerable<T> ExecuteAsync<T>(string command, DynamicParameters dp, CommandType cmdType = CommandType.StoredProcedure)
        {
            return sqlConnection.QueryUnbufferedAsync<T>(command, dp, commandType: cmdType);
        }

        public void Open()
        {
            sqlConnection.Open();
        }

        public async Task OpenAsync()
        {
            await sqlConnection.OpenAsync();
        }
    }
}
