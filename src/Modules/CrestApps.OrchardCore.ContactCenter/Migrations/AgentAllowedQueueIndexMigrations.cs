using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="AgentAllowedQueueIndex"/>.
/// </summary>
internal sealed class AgentAllowedQueueIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the agent allowed-queue index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AgentAllowedQueueIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26)),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AgentAllowedQueueIndex>(table => table
            .CreateIndex(
                "IDX_AgentAllowedQueueIndex_Queue",
                "DocumentId",
                "QueueId",
                "ItemId"),
            collection: ContactCenterStorage.CollectionName
        );

        return 1;
    }
}
