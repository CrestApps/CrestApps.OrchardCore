using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="QueueItemIndex"/>.
/// </summary>
internal sealed class QueueItemIndexMigrations : DataMigration
{
    // YesSql persists the QueueItemStatus enum as its underlying integer, so rows written under the former string
    // column hold that integer as text ("0", "1", ...). These invariant numeric strings match the stored
    // representation on every provider (comparing against the enum names would never match real rows).
    private static readonly string CompletedStatusValue =
        ((int)QueueItemStatus.Completed).ToString(CultureInfo.InvariantCulture);
    private static readonly string RemovedStatusValue =
        ((int)QueueItemStatus.Removed).ToString(CultureInfo.InvariantCulture);

    private readonly IStore _store;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueItemIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public QueueItemIndexMigrations(
        IStore store,
        IClock clock)
    {
        _store = store;
        _clock = clock;
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
            .Column<string>("ActivityClaimKey", column => column.NotNull().WithDefault(string.Empty).WithLength(26))
            .Column<QueueItemStatus>("Status")
            .Column<InteractionPriority>("Priority")
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<DateTime>("EnqueuedUtc", column => column.NotNull())
            .Column<DateTime?>("DequeuedUtc"),
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<QueueItemIndex>(table => table
            .CreateIndex("IDX_QueueItemIndex_DocumentId", "DocumentId", "QueueId", "Status", "ActivityItemId", "AgentId"),
            collection: ContactCenterConstants.CollectionName
        );

        // The claim constraint is created the same way the upgrade path creates it. Declaring it inline
        // instead would leave a freshly installed database with a different shape than an upgraded one, and
        // the two would then diverge again on the next rolling upgrade.
        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            SchemaBuilder,
            _store,
            typeof(QueueItemIndex),
            "UQ_QueueItemIndex_ActivityClaimKey",
            "ActivityClaimKey");

        await SchemaBuilder.AlterIndexTableAsync<QueueItemIndex>(table => table
            .CreateIndex("IDX_QueueItemIndex_Retention", "Status", "DequeuedUtc", "DocumentId"),
            collection: ContactCenterConstants.CollectionName
        );

        return 3;
    }

    /// <summary>
    /// Adds the time an item left the queue, which is what settled items are purged by. Purging by arrival
    /// time instead would delete an item the moment it was handled if it had waited longer than the window.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<QueueItemIndex>(table => table
            .AddColumn<DateTime?>("DequeuedUtc"),
            collection: ContactCenterConstants.CollectionName);

        // Adding a column does not re-project rows that already exist, and a settled item is never written
        // again, so without this the whole pre-upgrade backlog would keep a null dequeue time and could never
        // be purged. Legacy settled rows are dated from the upgrade so they age out a full window from now,
        // which is later than the truth and therefore never deletes anything early.
        await ContactCenterMigrationSql.AddRetentionColumnAsync(
            SchemaBuilder,
            _store,
            typeof(QueueItemIndex),
            "DequeuedUtc",
            _clock.UtcNow,
            $"{SchemaBuilder.Dialect.QuoteForColumnName("Status")} IN ({CompletedStatusValue}, {RemovedStatusValue})");

        await SchemaBuilder.AlterIndexTableAsync<QueueItemIndex>(table => table
            .CreateIndex("IDX_QueueItemIndex_Retention", "Status", "DequeuedUtc", "DocumentId"),
            collection: ContactCenterConstants.CollectionName);

        return 3;
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
            completedStatus.Value = CompletedStatusValue;
            command.Parameters.Add(completedStatus);

            var removedStatus = command.CreateParameter();
            removedStatus.ParameterName = "@RemovedStatus";
            removedStatus.Value = RemovedStatusValue;
            command.Parameters.Add(removedStatus);

            await command.ExecuteNonQueryAsync();
        }

        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            SchemaBuilder,
            _store,
            typeof(QueueItemIndex),
            "UQ_QueueItemIndex_ActivityClaimKey",
            "ActivityClaimKey");

        return 2;
    }

    private async Task EnsureLegacyRowsCanBeConstrainedAsync(
        string quotedTableName,
        string activityItemColumn,
        string itemIdColumn,
        string statusColumn)
    {
        var hasMissingIdentifiers = await ContactCenterMigrationSql.ExistsAsync(
            SchemaBuilder,
            $"""
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

        var hasDuplicateActivityClaims = await ContactCenterMigrationSql.ExistsAsync(
            SchemaBuilder,
            $"""
            SELECT 1
            FROM {quotedTableName}
            WHERE {statusColumn} NOT IN (@CompletedStatus, @RemovedStatus)
            GROUP BY {activityItemColumn}
            HAVING COUNT(*) > 1
            """,
            ("@CompletedStatus", CompletedStatusValue),
            ("@RemovedStatus", RemovedStatusValue));

        if (hasDuplicateActivityClaims)
        {
            throw new InvalidOperationException(
                "The Contact Center queue-item index contains multiple active items for one activity. Resolve the duplicate legacy queue items before enabling unique active queue-item claims.");
        }
    }

    /// <summary>
    /// Adds the predicate-led index the agent workspace reads. Every poll asks how many items are waiting in
    /// each queue the agent belongs to, and no existing index answers that: the composite leads with
    /// <c>DocumentId</c>, which serves join-back and delete-by-document but says nothing about a queue, and the
    /// retention index leads with <c>Status</c>, so the planner falls back to seeking that and walking every
    /// waiting item in the tenant to find the ones belonging to the queue being asked about — once per queue, on
    /// every poll of every signed-in agent.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom3Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<QueueItemIndex>(table => table
            .CreateIndex(
                "IDX_QueueItemIndex_WaitingByQueue",
                "QueueId",
                "Status",
                "DocumentId"),
            collection: ContactCenterConstants.CollectionName);

        return 4;
    }
}
