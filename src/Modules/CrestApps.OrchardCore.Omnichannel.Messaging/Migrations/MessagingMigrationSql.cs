using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Migrations;

/// <summary>
/// Dialect-portable SQL helpers for the messaging workspace index migrations. YesSql's schema builder can create an
/// index but not a unique one, so a uniqueness guarantee has to be emitted as SQL quoted through the active
/// dialect.
/// </summary>
internal static class MessagingMigrationSql
{
    /// <summary>
    /// Creates a unique index over the specified columns of a messaging workspace index table.
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
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentNullException.ThrowIfNull(store);

        var tableName = schemaBuilder.TablePrefix +
            schemaBuilder.TableNameConvention.GetIndexTable(indexType, MessagingStorage.CollectionName);
        var quotedTableName = schemaBuilder.Dialect.QuoteForTableName(tableName, store.Configuration.Schema);

        if (schemaBuilder.Dialect.PrefixIndex)
        {
            indexName = schemaBuilder.TablePrefix + indexName;
        }

        var quotedIndexName = schemaBuilder.Dialect.QuoteForColumnName(schemaBuilder.Dialect.FormatIndexName(indexName));
        var quotedColumns = string.Join(", ", columnNames.Select(schemaBuilder.Dialect.QuoteForColumnName));

        await using var command = schemaBuilder.Connection.CreateCommand();

        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = $"CREATE UNIQUE INDEX {quotedIndexName} ON {quotedTableName} ({quotedColumns})";

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Copies one column of a messaging workspace index table into another for every row, on the migration's own
    /// connection and transaction.
    /// </summary>
    /// <param name="schemaBuilder">The active schema builder.</param>
    /// <param name="store">The YesSql store.</param>
    /// <param name="indexType">The index type whose table is updated.</param>
    /// <param name="sourceColumn">The column the values are read from.</param>
    /// <param name="targetColumn">The column the values are written to.</param>
    public static async Task CopyColumnAsync(
        ISchemaBuilder schemaBuilder,
        IStore store,
        Type indexType,
        string sourceColumn,
        string targetColumn)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentNullException.ThrowIfNull(store);

        var tableName = schemaBuilder.TablePrefix +
            schemaBuilder.TableNameConvention.GetIndexTable(indexType, MessagingStorage.CollectionName);
        var quotedTableName = schemaBuilder.Dialect.QuoteForTableName(tableName, store.Configuration.Schema);
        var quotedSource = schemaBuilder.Dialect.QuoteForColumnName(sourceColumn);
        var quotedTarget = schemaBuilder.Dialect.QuoteForColumnName(targetColumn);

        await using var command = schemaBuilder.Connection.CreateCommand();

        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = $"UPDATE {quotedTableName} SET {quotedTarget} = {quotedSource}";

        await command.ExecuteNonQueryAsync();
    }
}
