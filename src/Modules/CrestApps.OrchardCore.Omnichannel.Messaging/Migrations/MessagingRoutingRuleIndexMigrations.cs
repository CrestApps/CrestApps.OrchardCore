using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Migrations;

/// <summary>
/// Creates the index table of the messaging routing rules.
/// </summary>
internal sealed class MessagingRoutingRuleIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the index table.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<MessagingRoutingRuleIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("DisplayText", column => column.Nullable().WithLength(255))
            .Column<bool>("Enabled")
            .Column<int>("Order"),
            collection: MessagingStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<MessagingRoutingRuleIndex>(table => table
            .CreateIndex(
                "IDX_MessagingRoutingRuleIndex_ItemId",
                "ItemId",
                "DocumentId"),
            collection: MessagingStorage.CollectionName);

        return 1;
    }
}
