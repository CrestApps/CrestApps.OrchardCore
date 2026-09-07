using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Models;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Subscriptions.Migrations;

/// <summary>
/// Creates the index table that backs the durable subscription agreements.
/// </summary>
public sealed class SubscriptionRecordMigrations : DataMigration
{
    /// <summary>
    /// Creates the initial schema.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<SubscriptionRecordIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Title")
            .Column<string>("OwnerId", column => column.WithLength(26))
            .Column<SubscriptionStatus>("Status")
            .Column<string>("ProviderKey", column => column.WithLength(64))
            .Column<string>("ProviderSubscriptionId", column => column.WithLength(128))
            .Column<string>("ReferenceType")
            .Column<string>("ReferenceId", column => column.WithLength(26))
            .Column<string>("CheckoutSessionId", column => column.WithLength(26))
            .Column<string>("ObligationId")
            .Column<string>("Currency", column => column.WithLength(8))
            .Column<decimal>("TotalAmount")
            .Column<DateTime>("CurrentPeriodEndUtc")
            .Column<DateTime>("NextBillingUtc", column => column.Nullable())
            .Column<DateTime>("GraceEndsUtc", column => column.Nullable())
            .Column<DateTime>("CreatedUtc")
            .Column<DateTime>("UpdatedUtc"),
            collection: SubscriptionConstants.SubscriptionCollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<SubscriptionRecordIndex>(table => table
            .CreateIndex("IDX_SubscriptionRecordIndex_ItemId", "ItemId"),
            collection: SubscriptionConstants.SubscriptionCollectionName
        );

        // A provider notification names only the provider's own id, so this lookup runs on every webhook.
        await SchemaBuilder.AlterIndexTableAsync<SubscriptionRecordIndex>(table => table
            .CreateIndex("IDX_SubscriptionRecordIndex_Provider", "ProviderSubscriptionId"),
            collection: SubscriptionConstants.SubscriptionCollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<SubscriptionRecordIndex>(table => table
            .CreateIndex("IDX_SubscriptionRecordIndex_Owner", "OwnerId", "Status"),
            collection: SubscriptionConstants.SubscriptionCollectionName
        );

        // The renewal sweep and the lapse sweep both scan by date, and must not read the whole table.
        await SchemaBuilder.AlterIndexTableAsync<SubscriptionRecordIndex>(table => table
            .CreateIndex("IDX_SubscriptionRecordIndex_NextBilling", "NextBillingUtc"),
            collection: SubscriptionConstants.SubscriptionCollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<SubscriptionRecordIndex>(table => table
            .CreateIndex("IDX_SubscriptionRecordIndex_Grace", "Status", "GraceEndsUtc"),
            collection: SubscriptionConstants.SubscriptionCollectionName
        );

        // Creating a subscription from a completed checkout looks itself up by obligation first, which is
        // what stops a checkout that completes twice from creating two agreements.
        await SchemaBuilder.AlterIndexTableAsync<SubscriptionRecordIndex>(table => table
            .CreateIndex("IDX_SubscriptionRecordIndex_Obligation", "CheckoutSessionId", "ObligationId"),
            collection: SubscriptionConstants.SubscriptionCollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<SubscriptionRecordIndex>(table => table
            .CreateIndex("IDX_SubscriptionRecordIndex_Reference", "ReferenceType", "ReferenceId"),
            collection: SubscriptionConstants.SubscriptionCollectionName
        );

        return 1;
    }
}
