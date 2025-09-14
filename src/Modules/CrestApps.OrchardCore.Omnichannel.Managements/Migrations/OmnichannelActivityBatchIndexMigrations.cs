using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

internal sealed class OmnichannelActivityBatchIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<OmnichannelActivityBatchIndex>(table => table
                .Column<string>("BatchId", column => column.WithLength(26))
                .Column<string>("Channel", column => column.WithLength(50))
                .Column<string>("DisplayText", column => column.WithLength(255))
                .Column<string>("Status", column => column.WithLength(20)),
            collection: OmnichannelConstants.CollectionName
        );

        // This SQL index is for locating incoming message from Omnichannel (Incoming SMS, Email, etc).
        await SchemaBuilder.AlterIndexTableAsync<OmnichannelActivityBatchIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivityBatchIndex_DocumentId",
                "DocumentId",
                "Channel",
                "DisplayText",
                "BatchId"
                ),
            collection: OmnichannelConstants.CollectionName
        );

        return 1;
    }
}
