using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the provider webhook inbox index schema.
/// </summary>
internal sealed class ProviderWebhookInboxMessageIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the inbox index table and its lookup and due-message indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ProviderWebhookInboxMessageIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ProviderName", column => column.WithLength(100))
            .Column<string>("DeliveryId", column => column.WithLength(256))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<DateTime>("NextAttemptUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<ProviderWebhookInboxMessageIndex>(table =>
        {
            table.CreateIndex(
                "IDX_ProviderWebhookInboxMessageIndex_Delivery",
                "ProviderName",
                "DeliveryId",
                "DocumentId");
            table.CreateIndex(
                "IDX_ProviderWebhookInboxMessageIndex_Due",
                "Status",
                "NextAttemptUtc",
                "DocumentId");
        },
            collection: ContactCenterConstants.CollectionName);

        return 1;
    }
}
