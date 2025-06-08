using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Subscriptions.Migrations;

public sealed class SubscriptionTransactionIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<SubscriptionTransactionIndex>(table => table
            .Column<string>("OwnerId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc")
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("SessionId", column => column.WithLength(26))
            .Column<string>("Status", column => column.WithLength(20))
            .Column<double>("Amount")
            .Column<string>("ContentItemVersionId", column => column.WithLength(26))
            .Column<string>("GatewayId", column => column.WithLength(50))
            .Column<string>("GatewayMode", column => column.WithLength(50))
            .Column<string>("GatewayTransactionId", column => column.WithLength(64))
            .Column<string>("ContentType", column => column.WithLength(ContentItemIndex.MaxContentTypeSize))
        );

        await SchemaBuilder.AlterIndexTableAsync<SubscriptionTransactionIndex>(table => table
            .CreateIndex("IDX_SubscriptionTransactionIndex_DocumentId",
                "DocumentId",
                "OwnerId",
                "CreatedUtc")
        );

        return 1;
    }
}
