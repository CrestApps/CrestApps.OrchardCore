using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Customers.Migrations;

/// <summary>
/// Creates the index of customers' loyalty points.
/// </summary>
public sealed class LoyaltyMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<LoyaltyAccountIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("CustomerId", column => column.WithLength(26))
            .Column<int>("Points"),
            collection: CustomersConstants.CollectionName
        );

        await IndexTableIndexCreation.CreateIfMissingAsync<LoyaltyAccountIndex>(SchemaBuilder, "IDX_LoyaltyAccountIndex_Customer", CustomersConstants.CollectionName, "CustomerId");
        await IndexTableIndexCreation.CreateIfMissingAsync<LoyaltyAccountIndex>(SchemaBuilder, "IDX_LoyaltyAccountIndex_ItemId", CustomersConstants.CollectionName, "ItemId");

        return 1;
    }
}
