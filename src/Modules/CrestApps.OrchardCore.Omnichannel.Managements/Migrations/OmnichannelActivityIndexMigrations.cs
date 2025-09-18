using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

internal sealed class OmnichannelActivityIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<OmnichannelActivityIndex>(table => table
            .Column<string>("ActivityId", column => column.WithLength(26))
            .Column<string>("Channel", column => column.WithLength(50))
            .Column<string>("ChannelEndpoint", column => column.WithLength(255))
            .Column<string>("PreferredDestination", column => column.WithLength(255))
            .Column<string>("AIProfileName", column => column.WithLength(255))
            .Column<string>("ContactContentItemId", column => column.WithLength(26))
            .Column<string>("ContactContentType", column => column.WithLength(255))
            .Column<string>("CampaignId", column => column.WithLength(26))
            .Column<string>("SubjectContentType", column => column.WithLength(26))
            .Column<DateTime>("ScheduledAt", column => column.NotNull())
            .Column<int>("Attempts", column => column.NotNull())
            .Column<string>("AssignedToId", column => column.WithLength(26))
            .Column<DateTime>("AssignedToUtc")
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
                "ChannelEndpoint",
                "PreferredDestination",
                "ScheduledAt"),
            collection: OmnichannelConstants.CollectionName
        );

        // This SQL index is for locating activities assigned to a specific user (My Activities view).
        await SchemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivityMyActivities_DocumentId",
                "DocumentId",
                "AssignedToId",
                "Status",
                "InteractionType",
                "ScheduledAt"),
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

        return 1;
    }
}
