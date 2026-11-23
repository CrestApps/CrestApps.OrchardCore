using System.Globalization;
using YesSql.Sql;

namespace CrestApps.OrchardCore.YesSql.Core.Migrations;

/// <summary>
/// Tells a migration whether a table is in the database, on every database engine YesSql supports.
/// </summary>
public static class TableExistence
{
    /// <summary>
    /// Gets whether a table is in the database, on the migration's own connection and transaction, so it sees the tables
    /// the migration made or dropped so far.
    /// </summary>
    /// <param name="schemaBuilder">The migration's schema builder.</param>
    /// <param name="table">The table's name, without the table prefix (such as <c>Payroll_PayRunIndex</c>).</param>
    /// <returns><see langword="true"/> when the table is there.</returns>
    public static async Task<bool> ExistsAsync(ISchemaBuilder schemaBuilder, string table)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentException.ThrowIfNullOrEmpty(table);

        var sql = schemaBuilder.Dialect.Name switch
        {
            "SqlServer" => "SELECT COUNT(1) FROM sys.tables WHERE name = @table",
            "PostgreSql" => "SELECT COUNT(1) FROM pg_tables WHERE tablename = @table",
            "MySql" => "SELECT COUNT(1) FROM information_schema.tables WHERE table_name = @table AND table_schema = DATABASE()",
            _ => "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = @table",
        };

        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = sql;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@table";
        parameter.Value = schemaBuilder.TablePrefix + table;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync();

        return result is not null and not DBNull && Convert.ToInt64(result, CultureInfo.InvariantCulture) > 0;
    }
}
