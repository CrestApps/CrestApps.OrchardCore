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
/// </remarks>
public static class IndexTableIndexCreation
{
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

        await schemaBuilder.AlterIndexTableAsync(indexType, table => table.CreateIndex(indexName, columns), collection);

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

        var tableName = schemaBuilder.TablePrefix + schemaBuilder.TableNameConvention.GetIndexTable(indexType, collection);

        // The name the index is stored under is not always the one passed to CreateIndex: the data layer prefixes it
        // on the engines that share one index namespace across tables, and shortens it where names are limited.
        var effectiveIndexName = schemaBuilder.Dialect.FormatIndexName(
            schemaBuilder.Dialect.PrefixIndex ? schemaBuilder.TablePrefix + indexName : indexName);

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

        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = sql;
        AddParameter(command, "@index", effectiveIndexName);
        AddParameter(command, "@table", tableName);

        var result = await command.ExecuteScalarAsync();

        return result is not null and not DBNull &&
            Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
