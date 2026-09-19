using System.Globalization;
using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.Core.ContactCenter.Models;
using OrchardCore.Modules;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Migrations;

/// <summary>
/// Creates the schema for the <see cref="QueueItemIndex"/>.
/// </summary>
internal sealed class QueueItemIndexMigrationsSchemaMigration : ISchemaMigration
{
    // YesSql persists the QueueItemStatus enum as its underlying integer, so rows written under the former string
    // column hold that integer as text ("0", "1", ...). These invariant numeric strings match the stored
    // representation on every provider (comparing against the enum names would never match real rows).
    private static readonly string CompletedStatusValue =
        ((int)QueueItemStatus.Completed).ToString(CultureInfo.InvariantCulture);

    private static readonly string RemovedStatusValue =
        ((int)QueueItemStatus.Removed).ToString(CultureInfo.InvariantCulture);

    private readonly IStore _store;

    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueItemIndexMigrationsSchemaMigration"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public QueueItemIndexMigrationsSchemaMigration(
        IStore store,
        TimeProvider timeProvider)
    {
        _store = store;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The stored name is the Orchard migration class's own name, so a database migrated under either
    /// host agrees on which version has already been applied.
    /// </remarks>
    public string Name => "QueueItemIndexMigrations";

    /// <summary>
    /// Creates the queue item index table.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<QueueItemIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("ActivityClaimKey", column => column.NotNull().WithDefault(string.Empty).WithLength(26))
            .Column<QueueItemStatus>("Status")
            .Column<InteractionPriority>("Priority")
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<DateTime>("EnqueuedUtc", column => column.NotNull())
            .Column<DateTime?>("DequeuedUtc"),
            collection: ContactCenterStorage.CollectionName
        );

        await builder.AlterIndexTableAsync<QueueItemIndex>(table => table
            .CreateIndex("IDX_QueueItemIndex_DocumentId", "DocumentId", "QueueId", "Status", "ActivityItemId", "AgentId"),
            collection: ContactCenterStorage.CollectionName
        );

        // The claim constraint is created the same way the upgrade path creates it. Declaring it inline
        // instead would leave a freshly installed database with a different shape than an upgraded one, and
        // the two would then diverge again on the next rolling upgrade.
        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            builder,
            _store,
            typeof(QueueItemIndex),
            "UQ_QueueItemIndex_ActivityClaimKey",
            "ActivityClaimKey");

        await builder.AlterIndexTableAsync<QueueItemIndex>(table => table
            .CreateIndex("IDX_QueueItemIndex_Retention", "Status", "DequeuedUtc", "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        return 3;
    }

    /// <summary>
    /// Moves the schema forward one step from an already-applied version.
    /// </summary>
    /// <param name="version">The version currently applied.</param>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at, or the same version when there is no step from it.</returns>
    public async Task<int> UpdateFromAsync(int version, ISchemaBuilder builder)
    {
        switch (version)
        {
            case 1:
                return await UpdateFrom1Async(builder);

            case 2:
                return await UpdateFrom2Async(builder);

            case 3:
                return await UpdateFrom3Async(builder);

            default:
                return version;
        }
    }

    private static async Task EnsureLegacyRowsCanBeConstrainedAsync(ISchemaBuilder builder,
        string quotedTableName,
        string activityItemColumn,
        string itemIdColumn,
        string statusColumn)
    {
        var hasMissingIdentifiers = await ContactCenterMigrationSql.ExistsAsync(
            builder,
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
            builder,
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
    /// Moves the schema from version 1 to the next version.
    /// </summary>
    /// <remarks>
    /// Kept as its own method, under the name the Orchard migration used, because the additive-only
    /// guard authorizes destructive steps per method. Folding every version into one switch would let
    /// a single authorization cover them all.
    /// </remarks>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    private async Task<int> UpdateFrom1Async(ISchemaBuilder builder)
    {
                    // Adds a portable unique active-queue-item constraint to existing indexes.
                    var tableName = builder.TablePrefix +
                        builder.TableNameConvention.GetIndexTable(
                            typeof(QueueItemIndex),
                            ContactCenterStorage.CollectionName);
                    var quotedTableName = builder.Dialect.QuoteForTableName(tableName, _store.Configuration.Schema);
                    var activityClaimColumn = builder.Dialect.QuoteForColumnName("ActivityClaimKey");
                    var activityItemColumn = builder.Dialect.QuoteForColumnName("ActivityItemId");
                    var itemIdColumn = builder.Dialect.QuoteForColumnName("ItemId");
                    var statusColumn = builder.Dialect.QuoteForColumnName("Status");

                    await EnsureLegacyRowsCanBeConstrainedAsync(builder,
                        quotedTableName,
                        activityItemColumn,
                        itemIdColumn,
                        statusColumn);

                    await builder.AlterIndexTableAsync<QueueItemIndex>(table =>
                        table.AddColumn<string>(
                            "ActivityClaimKey",
                            column => column.NotNull().WithDefault(string.Empty).WithLength(26)),
                        collection: ContactCenterStorage.CollectionName);

                    await using (var command = builder.Connection.CreateCommand())
                    {
                        command.Transaction = builder.Transaction;
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
                        builder,
                        _store,
                        typeof(QueueItemIndex),
                        "UQ_QueueItemIndex_ActivityClaimKey",
                        "ActivityClaimKey");

                    return 2;
                }

    /// <summary>
    /// Moves the schema from version 2 to the next version.
    /// </summary>
    /// <remarks>
    /// Kept as its own method, under the name the Orchard migration used, because the additive-only
    /// guard authorizes destructive steps per method. Folding every version into one switch would let
    /// a single authorization cover them all.
    /// </remarks>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    private async Task<int> UpdateFrom2Async(ISchemaBuilder builder)
    {
                    // Adds the time an item left the queue, which is what settled items are purged by. Purging by arrival
                    // time instead would delete an item the moment it was handled if it had waited longer than the window.
                    await builder.AlterIndexTableAsync<QueueItemIndex>(table => table
                        .AddColumn<DateTime?>("DequeuedUtc"),
                        collection: ContactCenterStorage.CollectionName);

                    // Adding a column does not re-project rows that already exist, and a settled item is never written
                    // again, so without this the whole pre-upgrade backlog would keep a null dequeue time and could never
                    // be purged. Legacy settled rows are dated from the upgrade so they age out a full window from now,
                    // which is later than the truth and therefore never deletes anything early.
                    await ContactCenterMigrationSql.AddRetentionColumnAsync(
                        builder,
                        _store,
                        typeof(QueueItemIndex),
                        "DequeuedUtc",
                        _timeProvider.GetUtcNow().UtcDateTime,
                        $"{builder.Dialect.QuoteForColumnName("Status")} IN ({CompletedStatusValue}, {RemovedStatusValue})");

                    await builder.AlterIndexTableAsync<QueueItemIndex>(table => table
                        .CreateIndex("IDX_QueueItemIndex_Retention", "Status", "DequeuedUtc", "DocumentId"),
                        collection: ContactCenterStorage.CollectionName);

                    return 3;
                }

    /// <summary>
    /// Moves the schema from version 3 to the next version.
    /// </summary>
    /// <remarks>
    /// Kept as its own method, under the name the Orchard migration used, because the additive-only
    /// guard authorizes destructive steps per method. Folding every version into one switch would let
    /// a single authorization cover them all.
    /// </remarks>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    private static async Task<int> UpdateFrom3Async(ISchemaBuilder builder)
    {
                    // Adds the predicate-led index the agent workspace reads. Every poll asks how many items are waiting in
                    // each queue the agent belongs to, and no existing index answers that: the composite leads with
                    // <c>DocumentId</c>, which serves join-back and delete-by-document but says nothing about a queue, and the
                    // retention index leads with <c>Status</c>, so the planner falls back to seeking that and walking every
                    // waiting item in the tenant to find the ones belonging to the queue being asked about — once per queue, on
                    // every poll of every signed-in agent.
                    await builder.AlterIndexTableAsync<QueueItemIndex>(table => table
                        .CreateIndex(
                            "IDX_QueueItemIndex_WaitingByQueue",
                            "QueueId",
                            "Status",
                            "DocumentId"),
                        collection: ContactCenterStorage.CollectionName);

                    return 4;
                }
}
