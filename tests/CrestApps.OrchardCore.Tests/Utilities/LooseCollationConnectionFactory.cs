using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Utilities;

/// <summary>
/// Opens SQLite connections that know a <see cref="CollationName"/> collation comparing text the way SQL Server's
/// default collation, <c>SQL_Latin1_General_CP1_CI_AS</c>, was measured to: without case, and with <c>AE</c> equal to
/// <c>Æ</c>, <c>TH</c> to <c>Þ</c> and <c>SS</c> to <c>ß</c>. SQLite's own comparisons are exact, so a test that needs
/// a database which answers a look-alike value with another row gives a column this collation with
/// <see cref="UseLooseCollationAsync"/>.
/// </summary>
public sealed class LooseCollationConnectionFactory : IConnectionFactory
{
    /// <summary>
    /// The name of the collation.
    /// </summary>
    public const string CollationName = "SQLSERVER_CI_AS";

    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="LooseCollationConnectionFactory"/> class.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    public LooseCollationConnectionFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        _connectionString = connectionString;
    }

    /// <inheritdoc/>
    public Type DbConnectionType => typeof(SqliteConnection);

    /// <inheritdoc/>
    public DbConnection CreateConnection()
        => Create(_connectionString);

    /// <summary>
    /// Rebuilds <paramref name="table"/> so that <paramref name="column"/> compares with the loose collation, keeping its
    /// rows. The table must have no index on the column.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    /// <param name="table">The table.</param>
    /// <param name="column">The text column.</param>
    public static async Task UseLooseCollationAsync(string connectionString, string table, string column)
    {
        await using var connection = Create(connectionString);
        await connection.OpenAsync();

        var create = (string)await ScalarAsync(connection, $"SELECT sql FROM sqlite_master WHERE type = 'table' AND name = '{table}'");
        var declaration = create is null ? null : Regex.Match(create, $@"[""\[`]?{Regex.Escape(column)}[""\]`]?\s+TEXT(\s*\(\d+\))?(\s+COLLATE\s+\w+)?", RegexOptions.IgnoreCase);

        if (declaration is not { Success: true })
        {
            throw new InvalidOperationException($"The table {table} has no text column {column}: {create}");
        }

        // YesSql declares SQLite text columns COLLATE NOCASE; the declared collation is replaced, not added to.
        var loose = create.Remove(declaration.Index, declaration.Length)
            .Insert(declaration.Index, $"{declaration.Value[..(declaration.Length - declaration.Groups[2].Length)]} COLLATE {CollationName}");

        await ExecuteAsync(connection, $"ALTER TABLE \"{table}\" RENAME TO \"{table}_exact\"");
        await ExecuteAsync(connection, loose);
        await ExecuteAsync(connection, $"INSERT INTO \"{table}\" SELECT * FROM \"{table}_exact\"");
        await ExecuteAsync(connection, $"DROP TABLE \"{table}_exact\"");
    }

    /// <summary>
    /// The name of the table whose name ends with <paramref name="suffix"/>, such as a collection's index table.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    /// <param name="suffix">The end of the table's name.</param>
    public static async Task<string> FindTableAsync(string connectionString, string suffix)
    {
        await using var connection = Create(connectionString);
        await connection.OpenAsync();

        return (string)await ScalarAsync(connection, $"SELECT name FROM sqlite_master WHERE type = 'table' AND name LIKE '%{suffix}'");
    }

    private static SqliteConnection Create(string connectionString)
    {
        var connection = new SqliteConnection(connectionString);
        connection.CreateCollation(CollationName, (left, right) => string.CompareOrdinal(Fold(left), Fold(right)));

        return connection;
    }

    private static string Fold(string value)
        => (value ?? string.Empty).ToUpperInvariant()
            .Replace("Æ", "AE", StringComparison.Ordinal)
            .Replace("Þ", "TH", StringComparison.Ordinal)
            .Replace("ß", "SS", StringComparison.Ordinal)
            .Replace("ẞ", "SS", StringComparison.Ordinal);

    private static async Task<object> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        return await command.ExecuteScalarAsync();
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
