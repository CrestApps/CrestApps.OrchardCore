using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="QueueItemIndex"/>.
/// </summary>
internal sealed class QueueItemIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the queue item index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<QueueItemIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<string>("Priority", column => column.WithLength(50))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<DateTime>("EnqueuedUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<QueueItemIndex>(table => table
            .CreateIndex("IDX_QueueItemIndex_DocumentId", "DocumentId", "QueueId", "Status", "ActivityItemId", "AgentId"),
            collection: ContactCenterConstants.CollectionName
        );

        return 1;
    }
}
