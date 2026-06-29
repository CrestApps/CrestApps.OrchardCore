using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

internal sealed class OmnichannelActivityIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates a new async.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<OmnichannelActivityIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Kind", column => column.WithLength(50))
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
            .Column<string>("AssignmentStatus", column => column.WithLength(50))
            .Column<string>("ReservationId", column => column.WithLength(26))
            .Column<string>("ReservedById", column => column.WithLength(26))
            .Column<DateTime>("ReservedUtc")
            .Column<DateTime>("ReservationExpiresUtc")
            .Column<string>("CreatedById", column => column.WithLength(26))
            .Column<string>("DispositionId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<string>("UrgencyLevel", column => column.WithLength(50))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<string>("InteractionType", column => column.WithLength(50)),
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

        return 2;
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
}
