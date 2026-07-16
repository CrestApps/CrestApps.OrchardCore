using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ActivityQueueGroupIndex"/>.
/// </summary>
internal sealed class ActivityQueueGroupIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the queue-group index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ActivityQueueGroupIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255)),
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<ActivityQueueGroupIndex>(table => table
            .CreateIndex("IDX_ActivityQueueGroupIndex_DocumentId", "DocumentId", "ItemId"),
            collection: ContactCenterConstants.CollectionName
        );

        return 1;
    }
}
