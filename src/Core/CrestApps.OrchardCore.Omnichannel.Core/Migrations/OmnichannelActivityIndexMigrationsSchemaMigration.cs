using CrestApps.Core.Omnichannel.Models;
using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Core.Migrations;

/// <summary>
/// Creates and evolves the schema for the omnichannel activity index, the table every activity list, batch
/// load, and reservation sweep reads.
/// </summary>
internal sealed class OmnichannelActivityIndexMigrationsSchemaMigration : ISchemaMigration
{
    private readonly IStore _store;

    private static readonly string[] _rebuiltColumnIndexNames =
    [
        "IDX_OmnichannelActivityMyActivities_DocumentId",
        "IDX_OmnichannelActivityMyActivities_BatchLoading",
        "IDX_OmnichannelActivity_Assignment",
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelActivityIndexMigrationsSchemaMigration"/> class.
    /// </summary>
    /// <param name="store">The YesSql store, used to resolve the schema the index table lives in.</param>
    public OmnichannelActivityIndexMigrationsSchemaMigration(IStore store)
    {
        _store = store;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The stored name is the Orchard migration class's own name, so a database migrated under either
    /// host agrees on which version has already been applied.
    /// </remarks>
    public string Name => "OmnichannelActivityIndexMigrations";

    /// <summary>
    /// Creates the omnichannel activity index table with the final set of columns and indexes.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<OmnichannelActivityIndex>(table => table
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
            .Column<bool>("AiEscalated"),
        collection: OmnichannelConstants.CollectionName
        );

        // This SQL index is for locating incoming message from Omnichannel (Incoming SMS, Email, etc).
        await builder.AlterIndexTableAsync<OmnichannelActivityIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivityIndex_DocumentId",
        "DocumentId",
        "Channel",
        "ChannelEndpointId",
        "PreferredDestination",
        "ScheduledUtc"),
        collection: OmnichannelConstants.CollectionName
        );

        // This SQL index is for locating activities assigned to a specific user (My Activities view).
        await builder.AlterIndexTableAsync<OmnichannelActivityIndex>(table => table
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
        await builder.AlterIndexTableAsync<OmnichannelActivityIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivityMyActivities_BatchLoading",
        "ContactContentType",
        "ContactContentItemId",
        "Status",
        "DocumentId"),
        collection: OmnichannelConstants.CollectionName
        );

        await builder.AlterIndexTableAsync<OmnichannelActivityIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivity_Assignment",
        "AssignmentStatus",
        "ReservationId",
        "ReservedById",
        "ScheduledUtc",
        "DocumentId"),
        collection: OmnichannelConstants.CollectionName
        );

        return 6;
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

            case 4:
                return await UpdateFrom4Async(builder);

            case 5:
                return await UpdateFrom5Async(builder);



            default:
                return version;
        }
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
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    private async Task<int> RebuildEnumColumnsAsync(ISchemaBuilder builder)
    {
        // On an engine that resolves an index by name alone the data layer's drop cannot see an index that
        // belongs to a named schema, so it silently drops nothing and the recreation below fails. The qualified
        // drop runs first and is a no-op wherever the data layer's own statement is already sufficient.
        foreach (var indexName in _rebuiltColumnIndexNames)
        {
            var qualifiedIndexName = SchemaQualifiedIndexDrop.TryGetQualifiedIndexName(
                builder,
                _store,
                typeof(OmnichannelActivityIndex),
                indexName,
                OmnichannelConstants.CollectionName);

            if (qualifiedIndexName is null)
            {
                continue;
            }

            await using var command = builder.Connection.CreateCommand();
            command.Transaction = builder.Transaction;
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
            builder.Transaction,
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
            builder,
            _store,
            "Kind",
            OmnichannelConstants.CollectionName);
        await IndexColumnRebuild.RebuildAsEnumColumnAsync<OmnichannelActivityIndex, ActivityAssignmentStatus>(
            builder,
            _store,
            "AssignmentStatus",
            OmnichannelConstants.CollectionName);
        await IndexColumnRebuild.RebuildAsEnumColumnAsync<OmnichannelActivityIndex, ActivityUrgencyLevel>(
            builder,
            _store,
            "UrgencyLevel",
            OmnichannelConstants.CollectionName);
        await IndexColumnRebuild.RebuildAsEnumColumnAsync<OmnichannelActivityIndex, ActivityStatus>(
            builder,
            _store,
            "Status",
            OmnichannelConstants.CollectionName);
        await IndexColumnRebuild.RebuildAsEnumColumnAsync<OmnichannelActivityIndex, ActivityInteractionType>(
            builder,
            _store,
            "InteractionType",
            OmnichannelConstants.CollectionName);

        await builder.AlterIndexTableAsync<OmnichannelActivityIndex>(table =>
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
    /// Moves the schema from version 1 to the next version.
    /// </summary>
    /// <remarks>
    /// Kept as its own method, under the name the Orchard migration used, because the additive-only
    /// guard authorizes destructive steps per method. Folding every version into one switch would let
    /// a single authorization cover them all.
    /// </remarks>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    private static async Task<int> UpdateFrom1Async(ISchemaBuilder builder)
    {
        // Adds Contact Center assignment and classification columns to the activity index.
        await builder.AlterIndexTableAsync<OmnichannelActivityIndex>(table =>
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

        await builder.AlterIndexTableAsync<OmnichannelActivityIndex>(table => table
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
    /// Moves the schema from version 2 to the next version.
    /// </summary>
    /// <remarks>
    /// Kept as its own method, under the name the Orchard migration used, because the additive-only
    /// guard authorizes destructive steps per method. Folding every version into one switch would let
    /// a single authorization cover them all.
    /// </remarks>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    private static Task<int> UpdateFrom2Async(ISchemaBuilder builder)
    {
        // Skips the superseded username-index migration.
        return Task.FromResult(4);
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
        // Removes usernames from the activity index because user presentation is resolved by shapes.
        await builder.AlterIndexTableAsync<OmnichannelActivityIndex>(table =>
        {
            table.DropColumn("AssignedToUsername");
            table.DropColumn("CreatedByUsername");
        },
        collection: OmnichannelConstants.CollectionName);

        return 4;
    }

    /// <summary>
    /// Moves the schema from version 4 to the next version.
    /// </summary>
    /// <remarks>
    /// Kept as its own method, under the name the Orchard migration used, because the additive-only
    /// guard authorizes destructive steps per method. Folding every version into one switch would let
    /// a single authorization cover them all.
    /// </remarks>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    private async Task<int> UpdateFrom4Async(ISchemaBuilder builder)
    {
        return await RebuildEnumColumnsAsync(builder);
    }

    /// <summary>
    /// Moves the schema from version 5 to the next version.
    /// </summary>
    /// <remarks>
    /// Kept as its own method, under the name the Orchard migration used, because the additive-only
    /// guard authorizes destructive steps per method. Folding every version into one switch would let
    /// a single authorization cover them all.
    /// </remarks>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    private static async Task<int> UpdateFrom5Async(ISchemaBuilder builder)
    {
        // Adds the AI-escalation flag used by containment reporting to count handoffs across channels.
        await builder.AlterIndexTableAsync<OmnichannelActivityIndex>(table =>
        {
            table.AddColumn<bool>("AiEscalated");
        },
        collection: OmnichannelConstants.CollectionName);

        return 6;
    }
}
