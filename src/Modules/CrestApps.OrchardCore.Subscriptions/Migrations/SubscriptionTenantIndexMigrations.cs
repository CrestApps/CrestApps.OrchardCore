using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Subscriptions.Migrations;

/// <summary>
/// Creates the YesSql map index table and database index for tenant onboarding lookups.
/// </summary>
public sealed class SubscriptionTenantIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the subscription tenant index table and its lookup index.
    /// </summary>
    /// <returns>The next migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<SubscriptionTenantIndex>(table => table
            .Column<string>("TenantName")
            .Column<string>("Recipe")
            .Column<string>("SessionId", column => column.WithLength(26))
        );

        await SchemaBuilder.AlterIndexTableAsync<SubscriptionTenantIndex>(table => table
            .CreateIndex("IDX_SubscriptionTenantIndex_DocumentId",
                "DocumentId",
                "TenantName",
                "SessionId")
        );

        return 1;
    }
}
