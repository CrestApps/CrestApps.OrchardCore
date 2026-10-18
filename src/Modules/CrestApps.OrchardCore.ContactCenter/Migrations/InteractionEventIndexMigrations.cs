using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="InteractionEventIndex"/> and enforces database-backed
/// idempotency-key uniqueness.
/// </summary>
internal sealed class InteractionEventIndexMigrations : DataMigration
{
    // An aggregate is usually one of the platform's own records, but a call with no interaction or call session --
    // a soft-phone call placed outside a queue -- is filed under the provider's id for the call, which arrives
    // verbatim from the switch. A Telnyx call control id is about 60 characters, and the original 26 made SQL Server
    // refuse the event; the refusal cancelled the session the soft phone's own call record was saved in, so the
    // call vanished and the phone hung up on its next refresh. 256 matches the call session's provider call id, and
    // keeps the aggregate index's key well inside SQL Server's index key limit.
    private const int AggregateIdLength = 256;

    private readonly IStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionEventIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public InteractionEventIndexMigrations(IStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Creates the interaction event index table and its supporting indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<InteractionEventIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("InteractionId", column => column.WithLength(26))
            .Column<string>("EventType", column => column.WithLength(128))
            .Column<string>("AggregateType", column => column.WithLength(128))
            .Column<string>("AggregateId", column => column.WithLength(AggregateIdLength))
            .Column<string>("CorrelationId", column => column.WithLength(26))
            .Column<string>("IdempotencyKey", column => column.WithLength(128))
            .Column<string>("IdempotencyClaimKey", column => column.NotNull().WithDefault(string.Empty).WithLength(128))
            .Column<DateTime>("OccurredUtc", column => column.NotNull()),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<InteractionEventIndex>(table => table
            .CreateIndex("IDX_InteractionEventIndex_Interaction",
                "InteractionId",
                "OccurredUtc",
                "EventType"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<InteractionEventIndex>(table => table
            .CreateIndex("IDX_InteractionEventIndex_Idempotency",
                "IdempotencyKey"),
            collection: ContactCenterStorage.CollectionName
        );

        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            SchemaBuilder,
            _store,
            typeof(InteractionEventIndex),
            "UQ_InteractionEventIndex_IdempotencyClaimKey",
            "IdempotencyClaimKey");

        return 2;
    }

    /// <summary>
    /// Adds the covering index the retention purge scans. Without it every terminating batch of the drain loop
    /// is a full scan of a table that grows with traffic, which is exactly the table size retention exists for.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<InteractionEventIndex>(table => table
            .CreateIndex(
                "IDX_InteractionEventIndex_Retention",
                "OccurredUtc",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        return 3;
    }

    /// <summary>
    /// Adds the index a workforce or payroll report reads the log through: one aggregate's events, such as one
    /// agent's state changes, in time order. Without it a report over one day reads the agent history of every
    /// agent since the log began, and finding the state an agent was in when the period opened is a scan.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom3Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<InteractionEventIndex>(table => table
            .CreateIndex(
                "IDX_InteractionEventIndex_Aggregate",
                "AggregateType",
                "AggregateId",
                "OccurredUtc",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        return 4;
    }

    /// <summary>
    /// Widens the aggregate column so an event filed under a provider's call id is stored rather than refused.
    /// </summary>
    /// <remarks>
    /// SQLite stores every text column as unbounded <c>TEXT</c>, so it never refused these events and the rebuild is
    /// a value-preserving no-op there; the engines that enforce a declared length (SQL Server, PostgreSQL, MySQL) are
    /// the ones it exists for. The aggregate index names the column, and SQLite refuses to drop a column an index
    /// refers to, so the index comes down before the rebuild and is recreated over the widened column.
    /// </remarks>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom4Async()
    {
        // On an engine that resolves an index by name alone the data layer's drop cannot see an index that belongs
        // to a named schema, so it silently drops nothing and the recreation below fails. The qualified drop runs
        // first and is a no-op wherever the data layer's own statement is already sufficient.
        var qualifiedIndexName = SchemaQualifiedIndexDrop.TryGetQualifiedIndexName(
            SchemaBuilder,
            _store,
            typeof(InteractionEventIndex),
            "IDX_InteractionEventIndex_Aggregate",
            ContactCenterStorage.CollectionName);

        if (qualifiedIndexName is not null)
        {
            await using var command = SchemaBuilder.Connection.CreateCommand();
            command.Transaction = SchemaBuilder.Transaction;
            command.CommandText = "drop index if exists " + qualifiedIndexName;

            await command.ExecuteNonQueryAsync();
        }

        // Tolerant because MySQL commits each schema change on its own and writes this drop without IF EXISTS, so an
        // attempt that stopped part-way would otherwise fail every activation from here on. The recreation below
        // runs on the strict builder, so an index that genuinely survived is still reported.
        var tolerantSchemaBuilder = new SchemaBuilder(
            _store.Configuration,
            SchemaBuilder.Transaction,
            throwOnError: false);

        await tolerantSchemaBuilder.AlterIndexTableAsync<InteractionEventIndex>(
            table => table.DropIndex("IDX_InteractionEventIndex_Aggregate"),
            collection: ContactCenterStorage.CollectionName);

        await IndexStringColumnRebuild.WidenAsync<InteractionEventIndex>(
            SchemaBuilder,
            _store,
            "AggregateId",
            AggregateIdLength,
            isNotNull: false,
            defaultValue: null,
            ContactCenterStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<InteractionEventIndex>(table => table
            .CreateIndex(
                "IDX_InteractionEventIndex_Aggregate",
                "AggregateType",
                "AggregateId",
                "OccurredUtc",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        return 5;
    }

    /// <summary>
    /// Adds the index an event is found by when it is read by its own identifier. Every catalog lookup by id
    /// filters on <c>ItemId</c>, and no existing index leads with it, so each such read scanned the whole log.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom5Async()
    {
        await IndexTableIndexCreation.CreateIfMissingAsync<InteractionEventIndex>(
            SchemaBuilder,
            "IDX_InteractionEventIndex_ItemId",
            ContactCenterStorage.CollectionName,
            "ItemId", "DocumentId");

        return 6;
    }

    /// <summary>
    /// Adds the index an unfiltered window read uses: every event of one aggregate type, such as every agent's state
    /// changes, inside a time range. The aggregate index leads with the aggregate's own id, so without an id filter
    /// it can seek only on the type and has to walk every aggregate's whole history to apply the range, which makes the
    /// cost of a workforce report grow with the size of the log instead of the size of the period. This index seeks on
    /// the type and the range together and carries the event type, so the read touches only the period's rows.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom6Async()
    {
        await IndexTableIndexCreation.CreateIfMissingAsync<InteractionEventIndex>(
            SchemaBuilder,
            "IDX_InteractionEventIndex_Window",
            ContactCenterStorage.CollectionName,
            "AggregateType", "OccurredUtc", "EventType", "DocumentId");

        return 7;
    }

    /// <summary>
    /// Adds the portable idempotency claim column and unique constraint to existing interaction event indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        var quotedTableName = ContactCenterMigrationSql.GetQuotedTableName(SchemaBuilder, _store, typeof(InteractionEventIndex));
        var claimColumn = SchemaBuilder.Dialect.QuoteForColumnName("IdempotencyClaimKey");
        var idempotencyColumn = SchemaBuilder.Dialect.QuoteForColumnName("IdempotencyKey");
        var itemIdColumn = SchemaBuilder.Dialect.QuoteForColumnName("ItemId");

        await EnsureLegacyRowsCanBeConstrainedAsync(quotedTableName, idempotencyColumn);

        await SchemaBuilder.AlterIndexTableAsync<InteractionEventIndex>(table => table
            .AddColumn<string>(
                "IdempotencyClaimKey",
                column => column.NotNull().WithDefault(string.Empty).WithLength(128)),
            collection: ContactCenterStorage.CollectionName);

        await using (var command = SchemaBuilder.Connection.CreateCommand())
        {
            command.Transaction = SchemaBuilder.Transaction;
            command.CommandText = $"""
                UPDATE {quotedTableName}
                SET {claimColumn} = CASE
                        WHEN {idempotencyColumn} IS NULL OR {idempotencyColumn} = '' THEN {itemIdColumn}
                        ELSE {idempotencyColumn}
                    END
                """;
            await command.ExecuteNonQueryAsync();
        }

        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            SchemaBuilder,
            _store,
            typeof(InteractionEventIndex),
            "UQ_InteractionEventIndex_IdempotencyClaimKey",
            "IdempotencyClaimKey");

        return 2;
    }

    private async Task EnsureLegacyRowsCanBeConstrainedAsync(string quotedTableName, string idempotencyColumn)
    {
        var hasDuplicateKeys = await ContactCenterMigrationSql.ExistsAsync(
            SchemaBuilder,
            $"""
            SELECT 1
            FROM {quotedTableName}
            WHERE {idempotencyColumn} IS NOT NULL AND {idempotencyColumn} <> ''
            GROUP BY {idempotencyColumn}
            HAVING COUNT(*) > 1
            """);

        if (hasDuplicateKeys)
        {
            throw new InvalidOperationException(
                "The Contact Center interaction event index contains multiple events with the same idempotency key. Resolve the duplicate legacy events before enabling the idempotency uniqueness constraint.");
        }
    }

    /// <summary>
    /// Adds the index reports read the records through, newest first by <c>OccurredUtc</c> a page at a time, so
    /// each page and each date range is found by seeking.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom7Async()
    {
        await CreateReportIndexAsync();

        return 8;
    }

    private Task<bool> CreateReportIndexAsync()
    {
        return IndexTableIndexCreation.CreateIfMissingAsync<InteractionEventIndex>(
            SchemaBuilder,
            "IDX_InteractionEventIndex_Report",
            ContactCenterStorage.CollectionName,
            "OccurredUtc", "Id");
    }
}
