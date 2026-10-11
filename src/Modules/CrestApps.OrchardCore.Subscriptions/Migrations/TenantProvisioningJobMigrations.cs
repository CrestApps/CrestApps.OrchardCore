using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Models;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Subscriptions.Migrations;

/// <summary>
/// Creates the index table that backs the durable tenant provisioning jobs.
/// </summary>
public sealed class TenantProvisioningJobMigrations : DataMigration
{
    /// <summary>
    /// Creates the initial schema.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<TenantProvisioningJobIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("CheckoutSessionId", column => column.WithLength(26))
            .Column<string>("SubscriptionId", column => column.WithLength(26))
            .Column<string>("OwnerId", column => column.WithLength(26))
            .Column<string>("TenantName", column => column.WithLength(128))
            .Column<TenantProvisioningStatus>("Status")
            .Column<DateTime>("NextAttemptUtc", column => column.Nullable())
            .Column<DateTime>("CreatedUtc"),
            collection: SubscriptionConstants.TenantProvisioningCollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<TenantProvisioningJobIndex>(table => table
            .CreateIndex("IDX_TenantProvisioningJobIndex_ItemId", "ItemId"),
            collection: SubscriptionConstants.TenantProvisioningCollectionName
        );

        // The completion handler looks the job up by checkout before creating one, which is what stops a
        // checkout that completes twice from trying to build the same site twice.
        await SchemaBuilder.AlterIndexTableAsync<TenantProvisioningJobIndex>(table => table
            .CreateIndex("IDX_TenantProvisioningJobIndex_Checkout", "CheckoutSessionId"),
            collection: SubscriptionConstants.TenantProvisioningCollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<TenantProvisioningJobIndex>(table => table
            .CreateIndex("IDX_TenantProvisioningJobIndex_Tenant", "TenantName"),
            collection: SubscriptionConstants.TenantProvisioningCollectionName
        );

        // The sweep runs every minute, so finding what is due must never read the whole table.
        await SchemaBuilder.AlterIndexTableAsync<TenantProvisioningJobIndex>(table => table
            .CreateIndex("IDX_TenantProvisioningJobIndex_Due", "Status", "NextAttemptUtc"),
            collection: SubscriptionConstants.TenantProvisioningCollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<TenantProvisioningJobIndex>(table => table
            .CreateIndex("IDX_TenantProvisioningJobIndex_Owner", "OwnerId"),
            collection: SubscriptionConstants.TenantProvisioningCollectionName
        );

        return 1;
    }
}
