using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Migrations;

internal sealed class OmnichannelMessageIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<OmnichannelMessageIndex>(table => table
                .Column<string>("Channel", column => column.WithLength(50))
                .Column<string>("CustomerAddress", column => column.WithLength(255))
                .Column<string>("ServiceAddress", column => column.WithLength(255))
                .Column<DateTime>("CreatedUtc", column => column.NotNull())
                .Column<bool>("IsInbound", column => column.NotNull().WithDefault(false)),
            collection: OmnichannelConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelMessageIndex>(table => table
            .CreateIndex("IDX_OmnichannelMessageIndex_DocumentId",
                "DocumentId",
                "Channel",
                "CustomerAddress",
                "ServiceAddress",
                "CreatedUtc"),
            collection: OmnichannelConstants.CollectionName
        );

        return 1;
    }
}
