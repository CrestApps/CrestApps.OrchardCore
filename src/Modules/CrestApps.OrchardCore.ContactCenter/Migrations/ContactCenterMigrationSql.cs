using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Provides shared, dialect-portable SQL helpers used by Contact Center index migrations to preflight
/// legacy rows and create unique constraints.
/// </summary>
internal static class ContactCenterMigrationSql
{
    /// <summary>
    /// Gets the quoted, prefixed index table name for the specified index type.
    /// </summary>
    /// <param name="schemaBuilder">The active schema builder.</param>
    /// <param name="store">The YesSql store.</param>
    /// <param name="indexType">The index type.</param>
    /// <returns>The quoted, prefixed table name.</returns>
    public static string GetQuotedTableName(ISchemaBuilder schemaBuilder, IStore store, Type indexType)
    {
        var tableName = schemaBuilder.TablePrefix +
            schemaBuilder.TableNameConvention.GetIndexTable(indexType, ContactCenterConstants.CollectionName);

        return schemaBuilder.Dialect.QuoteForTableName(tableName, store.Configuration.Schema);
    }

    /// <summary>
    /// Determines whether at least one row matches the specified query.
    /// </summary>
    /// <param name="schemaBuilder">The active schema builder.</param>
    /// <param name="commandText">The query text that returns at least one row when a match exists.</param>
    /// <param name="parameters">The optional named parameters.</param>
    /// <returns><see langword="true"/> when a matching row exists; otherwise <see langword="false"/>.</returns>
    public static async Task<bool> ExistsAsync(
        ISchemaBuilder schemaBuilder,
        string commandText,
        params (string Name, object Value)[] parameters)
    {
        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = commandText;

        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        return await command.ExecuteScalarAsync() is not null;
    }

    /// <summary>
    /// Builds a portable <c>CREATE UNIQUE INDEX</c> statement whose identifiers are quoted entirely through
    /// the supplied dialect, so the same migration code emits valid SQL on every supported database engine.
    /// </summary>
    /// <param name="dialect">The active SQL dialect.</param>
    /// <param name="tablePrefix">The configured table prefix, applied to the index name when the dialect requires globally-unique index names.</param>
    /// <param name="quotedTableName">The already dialect-quoted table name the index is created on.</param>
    /// <param name="indexName">The unqualified unique index name.</param>
    /// <param name="columnNames">The unquoted columns that participate in the unique constraint.</param>
    /// <returns>The dialect-quoted <c>CREATE UNIQUE INDEX</c> statement.</returns>
    public static string BuildCreateUniqueIndexStatement(
        ISqlDialect dialect,
        string tablePrefix,
        string quotedTableName,
        string indexName,
        params string[] columnNames)
    {
        ArgumentNullException.ThrowIfNull(dialect);

        if (dialect.PrefixIndex)
        {
            indexName = tablePrefix + indexName;
        }

        var quotedIndexName = dialect.QuoteForColumnName(dialect.FormatIndexName(indexName));
        var quotedColumns = string.Join(
            ", ",
            columnNames.Select(dialect.QuoteForColumnName));

        return $"CREATE UNIQUE INDEX {quotedIndexName} ON {quotedTableName} ({quotedColumns})";
    }

    /// <summary>
    /// Creates a unique index over the specified columns using dialect-aware quoting.
    /// </summary>
    /// <param name="schemaBuilder">The active schema builder.</param>
    /// <param name="store">The YesSql store.</param>
    /// <param name="indexType">The index type whose table receives the constraint.</param>
    /// <param name="indexName">The unqualified unique index name.</param>
    /// <param name="columnNames">The columns that participate in the unique constraint.</param>
    public static async Task CreateUniqueIndexAsync(
        ISchemaBuilder schemaBuilder,
        IStore store,
        Type indexType,
        string indexName,
        params string[] columnNames)
    {
        var quotedTableName = GetQuotedTableName(schemaBuilder, store, indexType);

        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = BuildCreateUniqueIndexStatement(
            schemaBuilder.Dialect,
            schemaBuilder.TablePrefix,
            quotedTableName,
            indexName,
            columnNames);
        await command.ExecuteNonQueryAsync();
    }
}
