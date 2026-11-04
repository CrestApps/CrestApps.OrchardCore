using CrestApps.OrchardCore.Webhooks.Core;
using CrestApps.OrchardCore.Webhooks.Core.Indexes;
using CrestApps.OrchardCore.Webhooks.Core.Models;
using OrchardCore.Data.Migration;
using YesSql.Sql;

// Orchard records which migration steps ran under the migration's full type name. This migration moved here from
// Commerce Webhooks, so it keeps its original namespace: a tenant that already ran it does not create the tables again.
namespace CrestApps.OrchardCore.Commerce.Webhooks.Migrations;

/// <summary>
/// Creates the indexes of webhook subscriptions and their deliveries.
/// </summary>
internal sealed class WebhookMigrations : DataMigration
{
    /// <summary>
    /// Creates the subscription and delivery index tables.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<WebhookSubscriptionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<bool>("IsEnabled")
            .Column<DateTime>("CreatedUtc"),
            collection: WebhookStorage.CollectionName
        );

        await SchemaBuilder.CreateMapIndexTableAsync<WebhookDeliveryIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("SubscriptionId", column => column.WithLength(26))
            .Column<string>("EventName", column => column.WithLength(64))
            .Column<WebhookDeliveryStatus>("Status")
            .Column<DateTime>("NextAttemptUtc", column => column.Nullable())
            .Column<DateTime>("CreatedUtc"),
            collection: WebhookStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<WebhookSubscriptionIndex>(table => table
            .CreateIndex("IDX_WebhookSubscriptionIndex_ItemId", "ItemId"),
            collection: WebhookStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<WebhookDeliveryIndex>(table =>
        {
            table.CreateIndex("IDX_WebhookDeliveryIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_WebhookDeliveryIndex_Due", "Status", "NextAttemptUtc");
            table.CreateIndex("IDX_WebhookDeliveryIndex_Subscription", "SubscriptionId", "CreatedUtc");
        }, collection: WebhookStorage.CollectionName);

        return 1;
    }
}
