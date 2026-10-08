using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="InteractionIndex"/>.
/// </summary>
internal sealed class InteractionIndexMigrations : DataMigration
{
    // The indexes over the queue column. SQLite refuses to drop a column an index refers to, so each comes down
    // before the widening rebuild and is recreated after it.
    private static readonly string[] _queueColumnIndexNames =
    [
        "IDX_InteractionIndex_DocumentId",
    ];

    private readonly IStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The store, for its table naming and content serializer.</param>
    public InteractionIndexMigrations(IStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Creates the interaction index table and its supporting indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<InteractionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<InteractionChannel>("Channel")
            .Column<InteractionDirection>("Direction")
            .Column<InteractionStatus>("Status")
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("ProviderName", column => column.WithLength(ContactCenterStorage.ProviderNameLength))
            .Column<string>("ProviderInteractionId", column => column.WithLength(128))
            .Column<string>("ProviderLegId", column => column.WithLength(128))
            .Column<string>("QueueId", column => column.WithLength(ContactCenterStorage.QueueIdLength))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<string>("CorrelationId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<DateTime>("EndedUtc"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<InteractionIndex>(table => table
            .CreateIndex("IDX_InteractionIndex_DocumentId",
                "DocumentId",
                "ItemId",
                "Status",
                "QueueId",
                "AgentId"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<InteractionIndex>(table => table
            .CreateIndex("IDX_InteractionIndex_Lookup",
                "ActivityItemId",
                "ProviderInteractionId",
                "ProviderLegId",
                "CorrelationId"),
            collection: ContactCenterStorage.CollectionName
        );

        return 1;
    }

    /// <summary>
    /// Adds after-call wrap-up timestamps used by handle-time reporting.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<InteractionIndex>(table =>
        {
            table.AddColumn<DateTime>("WrapUpStartedUtc");
            table.AddColumn<DateTime>("WrapUpCompletedUtc");
        },
            collection: ContactCenterStorage.CollectionName
        );

        return 2;
    }

    /// <summary>
    /// Adds the covering index the retention purge scans. Without it every terminating batch of the drain loop
    /// is a full scan of a table that grows with traffic, which is exactly the table size retention exists for.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<InteractionIndex>(table => table
            .CreateIndex(
                "IDX_InteractionIndex_Retention",
                "EndedUtc",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        return 3;
    }

    /// <summary>
    /// Adds the predicate-led index the reservation path reads. Routing asks how much live work an agent already
    /// holds before every offer, and the existing composite leads with <c>DocumentId</c>, which serves join-back
    /// and delete-by-document but answers nothing about an agent: without this index the question is answered by
    /// scanning every interaction the contact center has ever recorded, on every routing decision.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom3Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<InteractionIndex>(table => table
            .CreateIndex(
                "IDX_InteractionIndex_ActiveByAgent",
                "AgentId",
                "Status",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        return 4;
    }

    /// <summary>
    /// Adds the legal-hold flag the retention purge filters on. Held interactions must never be fetched by the
    /// age-based purge query, because a record the policy is forbidden to delete would otherwise be re-read on every
    /// batch and could stall the drain behind a page of undeletable rows.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom4Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<InteractionIndex>(table =>
            table.AddColumn<bool>("RecordingLegalHold", column => column.WithDefault(false)),
            collection: ContactCenterStorage.CollectionName
        );

        return 5;
    }

    /// <summary>
    /// Adds the recording-state columns and the covering index the secure-pause auto-resume guard scans. The guard
    /// force-resumes a recording that has stayed paused past the tenant's maximum secure-pause window; without a
    /// predicate-led index on the paused state and pause time, that periodic sweep would scan every interaction the
    /// contact center has ever recorded to find the handful currently paused.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom5Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<InteractionIndex>(table =>
        {
            table.AddColumn<RecordingState>("RecordingState", column => column.WithDefault((int)RecordingState.None));
            table.AddColumn<DateTime>("RecordingPausedUtc");
        },
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<InteractionIndex>(table => table
            .CreateIndex(
                "IDX_InteractionIndex_SecurePause",
                "RecordingState",
                "RecordingPausedUtc",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        return 6;
    }

    /// <summary>
    /// Merges the interactions an earlier defect stored twice, then makes a second document with the same id
    /// impossible.
    /// </summary>
    /// <remarks>
    /// Accepting an offer used to commit the unit of work part-way through and store the interaction again as a new
    /// document, so most accepted calls exist as two documents that each hold part of the call. They are merged
    /// first, on this step's own transaction, because the unique index cannot be created while they exist. A tenant
    /// without copies, including a new one, only runs the probe and creates the index. If anything fails the step
    /// rolls back as a whole and runs again on the next start.
    /// </remarks>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom6Async()
    {
        await InteractionDuplicateRepair.MergeDuplicatesAsync(SchemaBuilder, _store);

        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            SchemaBuilder,
            _store,
            typeof(InteractionIndex),
            "UQ_InteractionIndex_ItemId",
            "ItemId");

        return 7;
    }

    /// <summary>
    /// Widens the queue column so work routed under a campaign's virtual queue is stored rather than refused.
    /// </summary>
    /// <remarks>
    /// A campaign call's interaction records the campaign's virtual queue it was routed under. A campaign's virtual queue id is longer than the original 26 characters, so SQL Server refused the
    /// row. SQLite stores every text column as unbounded <c>TEXT</c>, so the rebuild is a value-preserving no-op
    /// there. SQLite also refuses to drop a column an index refers to, so the index over the queue
    /// column comes down before the rebuild and is recreated over the widened column.
    /// </remarks>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom7Async()
    {
        // On an engine that resolves an index by name alone the data layer's drop cannot see an index that belongs
        // to a named schema, so it silently drops nothing and the recreation below fails. The qualified drop runs
        // first and is a no-op wherever the data layer's own statement is already sufficient.
        foreach (var indexName in _queueColumnIndexNames)
        {
            var qualifiedIndexName = SchemaQualifiedIndexDrop.TryGetQualifiedIndexName(
                SchemaBuilder,
                _store,
                typeof(InteractionIndex),
                indexName,
                ContactCenterStorage.CollectionName);

            if (qualifiedIndexName is null)
            {
                continue;
            }

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

        await tolerantSchemaBuilder.AlterIndexTableAsync<InteractionIndex>(
            table => table.DropIndex("IDX_InteractionIndex_DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        await IndexStringColumnRebuild.WidenAsync<InteractionIndex>(
            SchemaBuilder,
            _store,
            "QueueId",
            ContactCenterStorage.QueueIdLength,
            isNotNull: false,
            defaultValue: null,
            ContactCenterStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<InteractionIndex>(table => table
            .CreateIndex("IDX_InteractionIndex_DocumentId",
                "DocumentId",
                "ItemId",
                "Status",
                "QueueId",
                "AgentId"),
            collection: ContactCenterStorage.CollectionName);

        return 8;
    }
}
