using CrestApps.OrchardCore.Commerce.Webhooks.Indexes;
using CrestApps.OrchardCore.Commerce.Webhooks.Models;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Commerce.Webhooks.Migrations;

/// <summary>
/// Creates the indexes of webhooks and their deliveries.
/// </summary>
internal sealed class WebhookMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<WebhookSubscriptionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<bool>("IsEnabled")
            .Column<DateTime>("CreatedUtc"),
            collection: WebhookConstants.CollectionName
        );

        await SchemaBuilder.CreateMapIndexTableAsync<WebhookDeliveryIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("SubscriptionId", column => column.WithLength(26))
            .Column<string>("EventName", column => column.WithLength(64))
            .Column<WebhookDeliveryStatus>("Status")
            .Column<DateTime>("NextAttemptUtc", column => column.Nullable())
            .Column<DateTime>("CreatedUtc"),
            collection: WebhookConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<WebhookSubscriptionIndex>(table => table
            .CreateIndex("IDX_WebhookSubscriptionIndex_ItemId", "ItemId"),
            collection: WebhookConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<WebhookDeliveryIndex>(table =>
        {
            table.CreateIndex("IDX_WebhookDeliveryIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_WebhookDeliveryIndex_Due", "Status", "NextAttemptUtc");
            table.CreateIndex("IDX_WebhookDeliveryIndex_Subscription", "SubscriptionId", "CreatedUtc");
        }, collection: WebhookConstants.CollectionName);

        return 1;
    }
}
