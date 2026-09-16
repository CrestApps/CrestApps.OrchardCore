using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Migrations;

/// <summary>
/// Dialect-portable SQL helpers for the SMS Portal index migrations. YesSql's schema builder can create an
/// index but not a unique one, so a uniqueness guarantee has to be emitted as SQL quoted through the active
/// dialect.
/// </summary>
internal static class SmsPortalMigrationSql
{
    /// <summary>
    /// Creates a unique index over the specified columns of an SMS Portal index table.
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
            schemaBuilder.TableNameConvention.GetIndexTable(indexType, SmsPortalStorage.CollectionName);
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
}
