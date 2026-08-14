using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Subscriptions.Migrations;

public sealed class SubscriptionTenantIndexMigrations : DataMigration
{
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
