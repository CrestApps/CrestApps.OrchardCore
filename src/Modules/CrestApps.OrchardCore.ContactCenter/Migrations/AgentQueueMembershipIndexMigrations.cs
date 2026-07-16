using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="AgentQueueMembershipIndex"/>.
/// </summary>
internal sealed class AgentQueueMembershipIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the agent queue membership index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AgentQueueMembershipIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<string>("PresenceStatus", column => column.WithLength(50))
            .Column<int>("MaxConcurrentInteractions"),
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AgentQueueMembershipIndex>(table => table
            .CreateIndex(
                "IDX_AgentQueueMembershipIndex_Queue",
                "DocumentId",
                "QueueId",
                "PresenceStatus",
                "ItemId"),
            collection: ContactCenterConstants.CollectionName
        );

        return 1;
    }
}
