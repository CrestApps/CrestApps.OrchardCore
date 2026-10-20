using CrestApps.OrchardCore.Commerce.Webhooks.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Commerce.Webhooks.Migrations;

/// <summary>
/// Creates the indexes of webhooks and their deliveries.
/// </summary>
public sealed class WebhookMigrations : DataMigration
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
            .Column<int>("Status")
            .Column<DateTime>("NextAttemptUtc", column => column.Nullable())
            .Column<DateTime>("CreatedUtc"),
            collection: WebhookConstants.CollectionName
        );

        await IndexTableIndexCreation.CreateIfMissingAsync<WebhookSubscriptionIndex>(SchemaBuilder, "IDX_WebhookSubscriptionIndex_ItemId", WebhookConstants.CollectionName, "ItemId");
        await IndexTableIndexCreation.CreateIfMissingAsync<WebhookDeliveryIndex>(SchemaBuilder, "IDX_WebhookDeliveryIndex_ItemId", WebhookConstants.CollectionName, "ItemId");
        await IndexTableIndexCreation.CreateIfMissingAsync<WebhookDeliveryIndex>(SchemaBuilder, "IDX_WebhookDeliveryIndex_Due", WebhookConstants.CollectionName, "Status", "NextAttemptUtc");
        await IndexTableIndexCreation.CreateIfMissingAsync<WebhookDeliveryIndex>(SchemaBuilder, "IDX_WebhookDeliveryIndex_Subscription", WebhookConstants.CollectionName, "SubscriptionId", "CreatedUtc");

        return 1;
    }
}
