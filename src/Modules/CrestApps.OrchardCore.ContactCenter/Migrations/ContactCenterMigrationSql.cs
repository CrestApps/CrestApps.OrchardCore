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

        if (schemaBuilder.Dialect.PrefixIndex)
        {
            indexName = schemaBuilder.TablePrefix + indexName;
        }

        var quotedIndexName = schemaBuilder.Dialect.QuoteForColumnName(
            schemaBuilder.Dialect.FormatIndexName(indexName));
        var quotedColumns = string.Join(
            ", ",
            columnNames.Select(schemaBuilder.Dialect.QuoteForColumnName));

        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = $"CREATE UNIQUE INDEX {quotedIndexName} ON {quotedTableName} ({quotedColumns})";
        await command.ExecuteNonQueryAsync();
    }
}
