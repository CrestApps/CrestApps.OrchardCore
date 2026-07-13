using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

internal sealed class OmnichannelActivityIndexMigrations : DataMigration
{
    private const int ReindexBatchSize = 100;

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
            .Column<string>("AssignedToUsername", column => column.WithLength(255))
            .Column<DateTime>("AssignedToUtc")
            .Column<string>("AssignmentStatus", column => column.WithLength(50))
            .Column<string>("ReservationId", column => column.WithLength(26))
            .Column<string>("ReservedById", column => column.WithLength(26))
            .Column<DateTime>("ReservedUtc")
            .Column<DateTime>("ReservationExpiresUtc")
            .Column<string>("CreatedById", column => column.WithLength(26))
            .Column<string>("CreatedByUsername", column => column.WithLength(255))
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

        return 3;
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
    /// Adds stored usernames used by cached report display-name shapes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<OmnichannelActivityIndex>(table =>
        {
            table.AddColumn<string>("AssignedToUsername", column => column.WithLength(255));
            table.AddColumn<string>("CreatedByUsername", column => column.WithLength(255));
        },
        collection: OmnichannelConstants.CollectionName);

        ShellScope.AddDeferredTask(ReindexActivitiesAsync);

        return 3;
    }

    private static async Task ReindexActivitiesAsync(ShellScope scope)
    {
        var store = scope.ServiceProvider.GetRequiredService<IStore>();
        var documentId = 0L;

        while (true)
        {
            await using var session = store.CreateSession();
            var activities = await session.Query<OmnichannelActivity, OmnichannelActivityIndex>(
                index => index.DocumentId > documentId,
                collection: OmnichannelConstants.CollectionName)
                .OrderBy(index => index.DocumentId)
                .Take(ReindexBatchSize)
                .ListAsync();

            if (!activities.Any())
            {
                return;
            }

            foreach (var activity in activities)
            {
                documentId = activity.Id;
                await session.SaveAsync(activity, collection: OmnichannelConstants.CollectionName);
            }

            await session.SaveChangesAsync();
        }
    }
}
