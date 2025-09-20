using CrestApps.OrchardCore.AI.Sms.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Sms.Migrations;

internal sealed class OminchannelActivityAIChatSessionIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<OminchannelActivityAIChatSessionIndex>(table => table
                .Column<string>("SessionId", column => column.WithLength(26))
                .Column<string>("ActivityId", column => column.WithLength(26)),
            collection: OmnichannelConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<OminchannelActivityAIChatSessionIndex>(table => table
            .CreateIndex("IDX_OminchannelActivityAIChatSessionIndex_DocumentId",
                "DocumentId",
                "ActivityId",
                "SessionId"),
            collection: OmnichannelConstants.CollectionName
        );

        return 1;
    }
}
