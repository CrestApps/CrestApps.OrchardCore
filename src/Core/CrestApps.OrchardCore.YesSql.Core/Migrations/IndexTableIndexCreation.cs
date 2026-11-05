using System.Data.Common;
using YesSql.Sql;

namespace CrestApps.OrchardCore.YesSql.Core.Migrations;

/// <summary>
/// Creates a database index on an index table unless an index of that name is already there.
/// </summary>
/// <remarks>
/// An upgrade step that adds an index fails when the index already exists, and on SQL Server and PostgreSQL that
/// failure cancels every migration still pending in the activation. The index can exist while the step's version
/// was never recorded when two app instances activate the same tenant at once: the second reads the version
/// before the first commits, waits on the first's schema lock, and then finds the index the first one created.
/// Checking first makes the step safe to repeat, so the second instance records the version instead of failing.
/// <para>
/// Every statement runs with <see cref="CommandTimeoutSeconds"/> instead of the provider's default of 30 seconds.
/// Building an index on a large table takes longer than that, and so does waiting on the schema lock of another
/// activation that is migrating the same tenant. A timeout cancels every migration still pending in the activation,
/// including the tables of features that were just enabled, and the same timeout then repeats at every start.
/// </para>
/// </remarks>
public static class IndexTableIndexCreation
{
    /// <summary>
    /// The number of seconds a statement of this helper may run before it is cancelled.
    /// </summary>
    public const int CommandTimeoutSeconds = 1800;

    /// <summary>
    /// Creates the index on the index table of <typeparamref name="TIndex"/> when it is missing.
    /// </summary>
    /// <typeparam name="TIndex">The index whose table carries the database index.</typeparam>
    /// <param name="schemaBuilder">The active schema builder.</param>
    /// <param name="indexName">The name of the database index, as passed to <c>CreateIndex</c>.</param>
    /// <param name="collection">The collection the index table belongs to; <see langword="null"/> for the default one.</param>
    /// <param name="columns">The indexed columns, in order.</param>
    /// <returns><see langword="true"/> when the index was created; <see langword="false"/> when it was already there.</returns>
    public static Task<bool> CreateIfMissingAsync<TIndex>(
        ISchemaBuilder schemaBuilder,
        string indexName,
        string collection,
        params string[] columns)
        => CreateIfMissingAsync(schemaBuilder, typeof(TIndex), indexName, collection, columns);

    /// <summary>
    /// Creates the index on the index table of <paramref name="indexType"/> when it is missing.
    /// </summary>
    /// <param name="schemaBuilder">The active schema builder.</param>
    /// <param name="indexType">The index whose table carries the database index.</param>
    /// <param name="indexName">The name of the database index, as passed to <c>CreateIndex</c>.</param>
    /// <param name="collection">The collection the index table belongs to; <see langword="null"/> for the default one.</param>
    /// <param name="columns">The indexed columns, in order.</param>
    /// <returns><see langword="true"/> when the index was created; <see langword="false"/> when it was already there.</returns>
    public static async Task<bool> CreateIfMissingAsync(
        ISchemaBuilder schemaBuilder,
        Type indexType,
        string indexName,
        string collection,
        params string[] columns)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentNullException.ThrowIfNull(indexType);
        ArgumentException.ThrowIfNullOrEmpty(indexName);
        ArgumentNullException.ThrowIfNull(columns);

        if (columns.Length == 0)
        {
            throw new ArgumentException("At least one column is required.", nameof(columns));
        }

        if (await ExistsAsync(schemaBuilder, indexType, indexName, collection))
        {
            return false;
        }

        var tableName = GetTableName(schemaBuilder, indexType, collection);
        var schemas = await ReadTableSchemasAsync(schemaBuilder, tableName);

        if (schemas is not null && schemas.Count != 1)
        {
            // The table is missing, or a table of that name is in more than one schema. The data layer's own
            // statement reports the first and picks the configured schema in the second.
            await schemaBuilder.AlterIndexTableAsync(indexType, table => table.CreateIndex(indexName, columns), collection);

            return true;
        }

        var dialect = schemaBuilder.Dialect;
        var quotedColumns = string.Join(", ", columns.Select(dialect.QuoteForColumnName));

        // The same statement the data layer builds for CreateIndex, run here so it can have a longer timeout.
        await using var command = CreateCommand(schemaBuilder);
        command.CommandText =
            $"CREATE INDEX {dialect.QuoteForColumnName(GetEffectiveIndexName(schemaBuilder, indexName))} " +
            $"ON {dialect.QuoteForTableName(tableName, schemas?[0])} ({quotedColumns})";

        await command.ExecuteNonQueryAsync();

        return true;
    }

    /// <summary>
    /// Determines whether the index table of <paramref name="indexType"/> already has an index of the given name.
    /// </summary>
    /// <param name="schemaBuilder">The active schema builder.</param>
    /// <param name="indexType">The index whose table carries the database index.</param>
    /// <param name="indexName">The name of the database index, as passed to <c>CreateIndex</c>.</param>
    /// <param name="collection">The collection the index table belongs to; <see langword="null"/> for the default one.</param>
    /// <returns><see langword="true"/> when the index exists.</returns>
    public static async Task<bool> ExistsAsync(ISchemaBuilder schemaBuilder, Type indexType, string indexName, string collection)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentNullException.ThrowIfNull(indexType);
        ArgumentException.ThrowIfNullOrEmpty(indexName);

        var tableName = GetTableName(schemaBuilder, indexType, collection);
        var effectiveIndexName = GetEffectiveIndexName(schemaBuilder, indexName);

        // Every query reads only the catalog and runs on the migration's own transaction, so it sees the indexes the
        // migration has created so far and needs no second connection.
        var sql = schemaBuilder.Dialect.Name switch
        {
            "SqlServer" =>
                "SELECT COUNT(1) FROM sys.indexes i INNER JOIN sys.tables t ON t.object_id = i.object_id " +
                "WHERE i.name = @index AND t.name = @table",
            "PostgreSql" =>
                "SELECT COUNT(1) FROM pg_indexes WHERE indexname = @index AND tablename = @table",
            "MySql" =>
                "SELECT COUNT(1) FROM information_schema.statistics " +
                "WHERE index_name = @index AND table_name = @table AND table_schema = DATABASE()",
            _ =>
                "SELECT COUNT(1) FROM sqlite_master WHERE type = 'index' AND name = @index AND tbl_name = @table",
        };

        await using var command = CreateCommand(schemaBuilder);
        command.CommandText = sql;
        AddParameter(command, "@index", effectiveIndexName);
        AddParameter(command, "@table", tableName);

        var result = await command.ExecuteScalarAsync();

        return result is not null and not DBNull &&
            Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    private static string GetTableName(ISchemaBuilder schemaBuilder, Type indexType, string collection)
        => schemaBuilder.TablePrefix + schemaBuilder.TableNameConvention.GetIndexTable(indexType, collection);

    // The name the index is stored under is not always the one passed to CreateIndex: the data layer prefixes it on
    // the engines that share one index namespace across tables, and shortens it where names are limited.
    private static string GetEffectiveIndexName(ISchemaBuilder schemaBuilder, string indexName)
        => schemaBuilder.Dialect.FormatIndexName(
            schemaBuilder.Dialect.PrefixIndex ? schemaBuilder.TablePrefix + indexName : indexName);

    /// <summary>
    /// Reads the schemas that hold a table of the given name, or returns <see langword="null"/> on the engines
    /// whose tables are not qualified by a schema.
    /// </summary>
    private static async Task<IReadOnlyList<string>> ReadTableSchemasAsync(ISchemaBuilder schemaBuilder, string tableName)
    {
        var sql = schemaBuilder.Dialect.Name switch
        {
            "SqlServer" => "SELECT SCHEMA_NAME(schema_id) FROM sys.tables WHERE name = @table",
            "PostgreSql" => "SELECT schemaname FROM pg_tables WHERE tablename = @table",
            _ => null,
        };

        if (sql is null)
        {
            return null;
        }

        await using var command = CreateCommand(schemaBuilder);
        command.CommandText = sql;
        AddParameter(command, "@table", tableName);

        var schemas = new List<string>();

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            schemas.Add(reader.GetString(0));
        }

        return schemas;
    }

    private static DbCommand CreateCommand(ISchemaBuilder schemaBuilder)
    {
        var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandTimeout = CommandTimeoutSeconds;

        return command;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
