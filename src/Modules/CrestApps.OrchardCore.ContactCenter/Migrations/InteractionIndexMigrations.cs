using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="InteractionIndex"/>.
/// </summary>
internal sealed class InteractionIndexMigrations : DataMigration
{
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
            .Column<string>("QueueId", column => column.WithLength(26))
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
}
