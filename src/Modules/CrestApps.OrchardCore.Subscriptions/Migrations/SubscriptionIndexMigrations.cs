using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Subscriptions.Migrations;

public sealed class SubscriptionIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<SubscriptionIndex>(table => table
            .Column<string>("OwnerId", column => column.WithLength(26))
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("SessionId", column => column.WithLength(26))
            .Column<string>("ContentItemVersionId", column => column.WithLength(26))
            .Column<string>("Gateway", column => column.WithLength(50))
            .Column<string>("GatewayMode", column => column.WithLength(50))
            .Column<string>("ContentType", column => column.WithLength(ContentItemIndex.MaxContentTypeSize))
            .Column<DateTime>("StartedAt")
            .Column<DateTime>("ExpiresAt")
        );

        await SchemaBuilder.AlterIndexTableAsync<SubscriptionIndex>(table => table
            .CreateIndex("IDX_SubscriptionIndex_DocumentId",
                "DocumentId",
                "OwnerId",
                "ContentItemId",
                "SessionId",
                "Gateway",
                "GatewayMode")
        );

        return 1;
    }
}
