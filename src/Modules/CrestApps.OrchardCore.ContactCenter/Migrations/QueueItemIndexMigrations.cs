using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="QueueItemIndex"/>.
/// </summary>
internal sealed class QueueItemIndexMigrations : DataMigration
{
    private readonly IStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueItemIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public QueueItemIndexMigrations(IStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Creates the queue item index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<QueueItemIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("ActivityClaimKey", column => column.NotNull().Unique().WithLength(26))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<string>("Priority", column => column.WithLength(50))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<DateTime>("EnqueuedUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<QueueItemIndex>(table => table
            .CreateIndex("IDX_QueueItemIndex_DocumentId", "DocumentId", "QueueId", "Status", "ActivityItemId", "AgentId"),
            collection: ContactCenterConstants.CollectionName
        );

        return 2;
    }

    /// <summary>
    /// Adds a portable unique active-queue-item constraint to existing indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        var tableName = SchemaBuilder.TablePrefix +
            SchemaBuilder.TableNameConvention.GetIndexTable(
                typeof(QueueItemIndex),
                ContactCenterConstants.CollectionName);
        var quotedTableName = SchemaBuilder.Dialect.QuoteForTableName(tableName, _store.Configuration.Schema);
        var activityClaimColumn = SchemaBuilder.Dialect.QuoteForColumnName("ActivityClaimKey");
        var activityItemColumn = SchemaBuilder.Dialect.QuoteForColumnName("ActivityItemId");
        var itemIdColumn = SchemaBuilder.Dialect.QuoteForColumnName("ItemId");
        var statusColumn = SchemaBuilder.Dialect.QuoteForColumnName("Status");

        await EnsureLegacyRowsCanBeConstrainedAsync(
            quotedTableName,
            activityItemColumn,
            itemIdColumn,
            statusColumn);

        await SchemaBuilder.AlterIndexTableAsync<QueueItemIndex>(table =>
            table.AddColumn<string>(
                "ActivityClaimKey",
                column => column.NotNull().WithDefault(string.Empty).WithLength(26)),
            collection: ContactCenterConstants.CollectionName);

        await using (var command = SchemaBuilder.Connection.CreateCommand())
        {
            command.Transaction = SchemaBuilder.Transaction;
            command.CommandText = $"""
                UPDATE {quotedTableName}
                SET {activityClaimColumn} = CASE
                    WHEN {statusColumn} IN (@CompletedStatus, @RemovedStatus) THEN {itemIdColumn}
                    ELSE {activityItemColumn}
                END
                """;

            var completedStatus = command.CreateParameter();
            completedStatus.ParameterName = "@CompletedStatus";
            completedStatus.Value = QueueItemStatus.Completed.ToString();
            command.Parameters.Add(completedStatus);

            var removedStatus = command.CreateParameter();
            removedStatus.ParameterName = "@RemovedStatus";
            removedStatus.Value = QueueItemStatus.Removed.ToString();
            command.Parameters.Add(removedStatus);

            await command.ExecuteNonQueryAsync();
        }

        var indexName = "UQ_QueueItemIndex_ActivityClaimKey";

        if (SchemaBuilder.Dialect.PrefixIndex)
        {
            indexName = SchemaBuilder.TablePrefix + indexName;
        }

        var quotedIndexName = SchemaBuilder.Dialect.QuoteForColumnName(
            SchemaBuilder.Dialect.FormatIndexName(indexName));
        await using var createIndexCommand = SchemaBuilder.Connection.CreateCommand();
        createIndexCommand.Transaction = SchemaBuilder.Transaction;
        createIndexCommand.CommandText =
            $"CREATE UNIQUE INDEX {quotedIndexName} ON {quotedTableName} ({activityClaimColumn})";
        await createIndexCommand.ExecuteNonQueryAsync();

        return 2;
    }

    private async Task EnsureLegacyRowsCanBeConstrainedAsync(
        string quotedTableName,
        string activityItemColumn,
        string itemIdColumn,
        string statusColumn)
    {
        var hasMissingIdentifiers = await ExistsAsync($"""
            SELECT 1
            FROM {quotedTableName}
            WHERE {itemIdColumn} IS NULL OR {itemIdColumn} = ''
               OR {activityItemColumn} IS NULL OR {activityItemColumn} = ''
            """);

        if (hasMissingIdentifiers)
        {
            throw new InvalidOperationException(
                "The Contact Center queue-item index contains rows without item or activity identifiers. Repair the legacy rows before enabling unique active queue-item claims.");
        }

        var hasDuplicateActivityClaims = await ExistsAsync(
            $"""
            SELECT 1
            FROM {quotedTableName}
            WHERE {statusColumn} NOT IN (@CompletedStatus, @RemovedStatus)
            GROUP BY {activityItemColumn}
            HAVING COUNT(*) > 1
            """,
            ("@CompletedStatus", QueueItemStatus.Completed.ToString()),
            ("@RemovedStatus", QueueItemStatus.Removed.ToString()));

        if (hasDuplicateActivityClaims)
        {
            throw new InvalidOperationException(
                "The Contact Center queue-item index contains multiple active items for one activity. Resolve the duplicate legacy queue items before enabling unique active queue-item claims.");
        }
    }

    private async Task<bool> ExistsAsync(
        string commandText,
        params (string Name, object Value)[] parameters)
    {
        await using var command = SchemaBuilder.Connection.CreateCommand();
        command.Transaction = SchemaBuilder.Transaction;
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
}
