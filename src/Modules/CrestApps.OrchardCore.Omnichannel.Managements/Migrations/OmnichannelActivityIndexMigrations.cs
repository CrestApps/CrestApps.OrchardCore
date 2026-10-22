using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

internal sealed class OmnichannelActivityIndexMigrations : DataMigration
{
    private readonly IStore _store;

    private static readonly string[] _rebuiltColumnIndexNames =
    [
        "IDX_OmnichannelActivityMyActivities_DocumentId",
        "IDX_OmnichannelActivityMyActivities_BatchLoading",
        "IDX_OmnichannelActivity_Assignment",
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelActivityIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store, used to resolve the schema the index table lives in.</param>
    public OmnichannelActivityIndexMigrations(IStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Creates a new async.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<OmnichannelActivityIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<ActivityKind>("Kind")
            .Column<string>("Source", column => column.WithLength(50))
            .Column<string>("Channel", column => column.WithLength(50))
            .Column<string>("ChannelEndpointId", column => column.WithLength(26))
            .Column<string>("PreferredDestination", column => column.WithLength(255))
            .Column<string>("AIProfileName", column => column.WithLength(255))
            .Column<string>("ContactContentItemId", column => column.WithLength(26))
            .Column<string>("ContactContentType", column => column.WithLength(255))
            .Column<string>("CampaignId", column => column.WithLength(26))
            .Column<string>("DialerProfileId", column => column.Nullable().WithLength(26))
            .Column<string>("SubjectContentType", column => column.WithLength(26))
            .Column<DateTime>("ScheduledUtc", column => column.NotNull())
            .Column<DateTime>("CompletedUtc")
            .Column<int>("Attempts", column => column.NotNull())
            .Column<string>("AssignedToId", column => column.WithLength(26))
            .Column<DateTime>("AssignedToUtc")
            .Column<ActivityAssignmentStatus>("AssignmentStatus")
            .Column<string>("ReservationId", column => column.WithLength(26))
            .Column<string>("ReservedById", column => column.WithLength(26))
            .Column<DateTime>("ReservedUtc")
            .Column<DateTime>("ReservationExpiresUtc")
            .Column<string>("CreatedById", column => column.WithLength(26))
            .Column<string>("DispositionId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<ActivityUrgencyLevel>("UrgencyLevel")
            .Column<ActivityStatus>("Status")
            .Column<ActivityInteractionType>("InteractionType")
            .Column<bool>("AiEscalated")
            .Column<string>("TerminalReasonCode", column => column.Nullable().WithLength(64)),
        collection: OmnichannelConstants.CollectionName
        );

        // This SQL index is for locating incoming message from Omnichannel (Incoming SMS, Email, etc).
        await SchemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivityIndex_DocumentId",
        "DocumentId",
        "Channel",
        "ChannelEndpointId",
        "PreferredDestination",
        "ScheduledUtc"),
        collection: OmnichannelConstants.CollectionName
        );

        // This SQL index is for locating activities assigned to a specific user (My Activities view).
        await SchemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivityMyActivities_DocumentId",
        "DocumentId",
        "AssignedToId",
        "Status",
        "AssignmentStatus",
        "InteractionType",
        "ScheduledUtc"),
        collection: OmnichannelConstants.CollectionName
        );

        // This SQL index is for locating duplicate activities during batch loading.
        await SchemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivityMyActivities_BatchLoading",
        "ContactContentType",
        "ContactContentItemId",
        "Status",
        "DocumentId"),
        collection: OmnichannelConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivity_Assignment",
        "AssignmentStatus",
        "ReservationId",
        "ReservedById",
        "ScheduledUtc",
        "DocumentId"),
        collection: OmnichannelConstants.CollectionName
        );

        // This SQL index is for locating an activity by its own identifier.
        await SchemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivityIndex_ItemId",
        "ItemId",
        "DocumentId"),
        collection: OmnichannelConstants.CollectionName
        );

        await CreateReportIndexAsync();
        await CreateLookupIndexesAsync();

        return 11;
    }

    /// <summary>
    /// Adds Contact Center assignment and classification columns to the activity index.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table =>
        {
            table.AddColumn<string>("Kind", column => column.WithLength(50));
            table.AddColumn<string>("Source", column => column.WithLength(50));
            table.AddColumn<string>("AssignmentStatus", column => column.WithLength(50));
            table.AddColumn<string>("ReservationId", column => column.WithLength(26));
            table.AddColumn<string>("ReservedById", column => column.WithLength(26));
            table.AddColumn<DateTime>("ReservedUtc");
            table.AddColumn<DateTime>("ReservationExpiresUtc");
        },
        collection: OmnichannelConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivity_Assignment",
                "AssignmentStatus",
                "ReservationId",
                "ReservedById",
                "ScheduledUtc",
                "DocumentId"),
            collection: OmnichannelConstants.CollectionName);

        return 2;
    }

    /// <summary>
    /// Skips the superseded username-index migration.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public static int UpdateFrom2()
    {
        return 4;
    }

    /// <summary>
    /// Removes usernames from the activity index because user presentation is resolved by shapes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom3Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table =>
        {
            table.DropColumn("AssignedToUsername");
            table.DropColumn("CreatedByUsername");
        },
        collection: OmnichannelConstants.CollectionName);

        return 4;
    }

    /// <summary>
    /// Declares the enum-valued columns as the integer columns they have always held, and restores the
    /// assignment column to the assigned-activity index, so an upgraded tenant matches a freshly created one.
    /// </summary>
    /// <remarks>
    /// Every one of these columns was created as text while the index property behind it is an enum, and YesSql
    /// writes an enum as its underlying integer whatever the column says. SQLite reconciles the mismatch through
    /// column affinity, so an upgraded tenant behaves correctly there and the divergence stays invisible; an
    /// engine that does not coerce rejects the write, so the same tenant cannot run at all. The assignment
    /// column is likewise missing from the index that filters an agent's own activities, so that screen reads
    /// more rows than it selects on exactly the tenants that have the most history.
    /// </remarks>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom4Async()
    {
        // On an engine that resolves an index by name alone the data layer's drop cannot see an index that
        // belongs to a named schema, so it silently drops nothing and the recreation below fails. The qualified
        // drop runs first and is a no-op wherever the data layer's own statement is already sufficient.
        foreach (var indexName in _rebuiltColumnIndexNames)
        {
            var qualifiedIndexName = SchemaQualifiedIndexDrop.TryGetQualifiedIndexName(
                SchemaBuilder,
                _store,
                typeof(OmnichannelActivityIndex),
                indexName,
                OmnichannelConstants.CollectionName);

            if (qualifiedIndexName is null)
            {
                continue;
            }

            await using var command = SchemaBuilder.Connection.CreateCommand();
            command.Transaction = SchemaBuilder.Transaction;
            command.CommandText = "drop index if exists " + qualifiedIndexName;

            await command.ExecuteNonQueryAsync();
        }

        // SQLite refuses to drop a column an index refers to, and the rebuild has to drop the old column to
        // replace it, so every index over a rebuilt column comes down first and is recreated afterwards. The
        // drops are tolerant because MySQL commits each schema change on its own and writes this drop without
        // IF EXISTS, so an attempt that stopped part-way would otherwise fail every activation from here on. A
        // drop that genuinely fails is still reported, because the recreation below runs on the strict builder.
        // Each index is dropped in its own alter so a swallowed failure of one — a re-run meeting an index a
        // previous attempt already dropped — cannot suppress the drop of the next: the data layer runs every
        // statement of a single alter under one try, so batching the drops would let the first failure strand
        // the rest, and a surviving index would then make its own recreation below fail every activation.
        var tolerantSchemaBuilder = new SchemaBuilder(
            _store.Configuration,
            SchemaBuilder.Transaction,
            throwOnError: false);

        await tolerantSchemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(
            table => table.DropIndex("IDX_OmnichannelActivityMyActivities_DocumentId"),
            collection: OmnichannelConstants.CollectionName);

        await tolerantSchemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(
            table => table.DropIndex("IDX_OmnichannelActivityMyActivities_BatchLoading"),
            collection: OmnichannelConstants.CollectionName);

        await tolerantSchemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(
            table => table.DropIndex("IDX_OmnichannelActivity_Assignment"),
            collection: OmnichannelConstants.CollectionName);

        await IndexColumnRebuild.RebuildAsEnumColumnAsync<OmnichannelActivityIndex, ActivityKind>(
            SchemaBuilder,
            _store,
            "Kind",
            OmnichannelConstants.CollectionName);
        await IndexColumnRebuild.RebuildAsEnumColumnAsync<OmnichannelActivityIndex, ActivityAssignmentStatus>(
            SchemaBuilder,
            _store,
            "AssignmentStatus",
            OmnichannelConstants.CollectionName);
        await IndexColumnRebuild.RebuildAsEnumColumnAsync<OmnichannelActivityIndex, ActivityUrgencyLevel>(
            SchemaBuilder,
            _store,
            "UrgencyLevel",
            OmnichannelConstants.CollectionName);
        await IndexColumnRebuild.RebuildAsEnumColumnAsync<OmnichannelActivityIndex, ActivityStatus>(
            SchemaBuilder,
            _store,
            "Status",
            OmnichannelConstants.CollectionName);
        await IndexColumnRebuild.RebuildAsEnumColumnAsync<OmnichannelActivityIndex, ActivityInteractionType>(
            SchemaBuilder,
            _store,
            "InteractionType",
            OmnichannelConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table =>
        {
            table.CreateIndex("IDX_OmnichannelActivityMyActivities_DocumentId",
                "DocumentId",
                "AssignedToId",
                "Status",
                "AssignmentStatus",
                "InteractionType",
                "ScheduledUtc");
            table.CreateIndex("IDX_OmnichannelActivityMyActivities_BatchLoading",
                "ContactContentType",
                "ContactContentItemId",
                "Status",
                "DocumentId");
            table.CreateIndex("IDX_OmnichannelActivity_Assignment",
                "AssignmentStatus",
                "ReservationId",
                "ReservedById",
                "ScheduledUtc",
                "DocumentId");
        },
        collection: OmnichannelConstants.CollectionName);

        return 5;
    }

    /// <summary>
    /// Adds the AI-escalation flag used by containment reporting to count handoffs across channels.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom5Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table =>
        {
            table.AddColumn<bool>("AiEscalated");
        },
        collection: OmnichannelConstants.CollectionName);

        return 6;
    }

    /// <summary>
    /// Adds the terminal reason, so reports can count activities that ended for a stated reason, such as a number
    /// that is not in service, without loading every activity.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom6Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table =>
        {
            table.AddColumn<string>("TerminalReasonCode", column => column.Nullable().WithLength(64));
        },
        collection: OmnichannelConstants.CollectionName);

        return 7;
    }

    /// <summary>
    /// Adds the index an activity is found by when it is read by its own identifier. Every catalog lookup by id
    /// filters on <c>ItemId</c>, and no existing index leads with it, so each such read scanned every activity.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom7Async()
    {
        await IndexTableIndexCreation.CreateIfMissingAsync<OmnichannelActivityIndex>(
            SchemaBuilder,
            "IDX_OmnichannelActivityIndex_ItemId",
            OmnichannelConstants.CollectionName,
            "ItemId", "DocumentId");

        return 8;
    }

    /// <summary>
    /// Adds the dialer profile an activity is dialed under, so Manage Activities can filter by it. Activities saved
    /// before this step are filled in by <see cref="BackgroundTasks.ActivityDialerProfileIndexBackfillBackgroundTask"/>, outside
    /// activation, because re-indexing every open dialer record during activation would hold up the tenant.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom8Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table =>
        {
            table.AddColumn<string>("DialerProfileId", column => column.Nullable().WithLength(26));
        },
        collection: OmnichannelConstants.CollectionName);

        return 9;
    }

    /// <summary>
    /// Adds the index reports read the records through, newest first by <c>CreatedUtc</c> a page at a time, so
    /// each page and each date range is found by seeking.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom9Async()
    {
        await CreateReportIndexAsync();

        return 10;
    }

    /// <summary>
    /// Adds the indexes the frequent activity lookups seek through. Each of them filtered on a column no index led
    /// with, so on a small database every run read the whole activity table: the background sweeps by status, which
    /// run every minute or few even when nothing is due; the supervisor dashboard's counts per campaign, polled every
    /// thirty seconds; the inbound lookups by the caller's number; and the activity lists of a contact and of a user.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom10Async()
    {
        await CreateLookupIndexesAsync();

        return 11;
    }

    private async Task CreateLookupIndexesAsync()
    {
        // The status sweeps: orphan recovery, the automated, expiry, SMS and stranded call sweeps, and the status
        // filter of Manage Activities. Each asks for a few open statuses of one interaction type, often on one
        // channel, a page at a time by document id.
        await IndexTableIndexCreation.CreateIfMissingAsync<OmnichannelActivityIndex>(
            SchemaBuilder,
            "IDX_OmnichannelActivityIndex_StatusSweep",
            OmnichannelConstants.CollectionName,
            "Status", "InteractionType", "Channel", "DocumentId");

        // The waiting records of a campaign, counted and read earliest first by the dispatch diagnostics and the
        // queue inventory.
        await IndexTableIndexCreation.CreateIfMissingAsync<OmnichannelActivityIndex>(
            SchemaBuilder,
            "IDX_OmnichannelActivityIndex_Campaign",
            OmnichannelConstants.CollectionName,
            "CampaignId", "Status", "ScheduledUtc", "DocumentId");

        // The activities at a number: a returning caller, a promised callback, an inbound message's conversation.
        await IndexTableIndexCreation.CreateIfMissingAsync<OmnichannelActivityIndex>(
            SchemaBuilder,
            "IDX_OmnichannelActivityIndex_Destination",
            OmnichannelConstants.CollectionName,
            "PreferredDestination", "Channel", "CreatedUtc", "DocumentId");

        // The activities of a contact, on its page, its account's page, lead conversion and export. The batch loading
        // index leads with the contact type, which these lookups do not know.
        await IndexTableIndexCreation.CreateIfMissingAsync<OmnichannelActivityIndex>(
            SchemaBuilder,
            "IDX_OmnichannelActivityIndex_Contact",
            OmnichannelConstants.CollectionName,
            "ContactContentItemId", "Status", "InteractionType", "DocumentId");

        // My Activities: a user's open manual activities, latest scheduled first.
        await IndexTableIndexCreation.CreateIfMissingAsync<OmnichannelActivityIndex>(
            SchemaBuilder,
            "IDX_OmnichannelActivityIndex_AssignedTo",
            OmnichannelConstants.CollectionName,
            "AssignedToId", "Status", "InteractionType", "ScheduledUtc");
    }

    private Task<bool> CreateReportIndexAsync()
    {
        return IndexTableIndexCreation.CreateIfMissingAsync<OmnichannelActivityIndex>(
            SchemaBuilder,
            "IDX_OmnichannelActivityIndex_Report",
            OmnichannelConstants.CollectionName,
            "CreatedUtc", "Id");
    }
}
