using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

internal sealed class OmnichannelActivityBatchIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates a new async.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<OmnichannelActivityBatchIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("DisplayText", column => column.WithLength(255))
            .Column<string>("Source", column => column.WithLength(50))
            .Column<string>("Status", column => column.WithLength(20)),
        collection: OmnichannelConstants.CollectionName
        );

        // This SQL index is for locating incoming message from Omnichannel (Incoming SMS, Email, etc).
        await SchemaBuilder.AlterIndexTableAsync<OmnichannelActivityBatchIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivityBatchIndex_DocumentId",
        "DocumentId",
        "DisplayText",
        "ItemId"
        ),
        collection: OmnichannelConstants.CollectionName
        );

        return 2;
    }

    /// <summary>
    /// Adds the activity batch source column.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<OmnichannelActivityBatchIndex>(table =>
        {
            table.AddColumn<string>("Source", column => column.WithLength(50));
        },
        collection: OmnichannelConstants.CollectionName);

        return 2;
    }
}
