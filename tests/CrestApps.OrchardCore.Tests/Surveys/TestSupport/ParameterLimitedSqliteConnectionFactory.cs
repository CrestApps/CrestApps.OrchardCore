using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// Opens SQLite connections that can be held to SQL Server's limit of 2,100 parameters per statement. SQLite accepts far
/// more, so without the limit a query with an <c>IN</c> list of every survey id passes here and fails in production.
/// </summary>
internal sealed class ParameterLimitedSqliteConnectionFactory : IConnectionFactory
{
    /// <summary>
    /// The most parameters SQL Server accepts in one statement.
    /// </summary>
    public const int SqlServerLimit = 2100;

    private readonly string _connectionString;

    public ParameterLimitedSqliteConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Gets or sets the limit applied to the connections opened from now on, or <see langword="null"/> for none.
    /// </summary>
    public int? ParameterLimit { get; set; }

    public Type DbConnectionType => typeof(SqliteConnection);

    public DbConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.StateChange += OnStateChange;

        return connection;
    }

    private void OnStateChange(object sender, StateChangeEventArgs e)
    {
        if (e.CurrentState != ConnectionState.Open || sender is not SqliteConnection connection || ParameterLimit is not int limit)
        {
            return;
        }

        SQLitePCL.raw.sqlite3_limit(connection.Handle, SQLitePCL.raw.SQLITE_LIMIT_VARIABLE_NUMBER, limit);
    }
}
