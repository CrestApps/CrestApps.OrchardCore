using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Customers.Migrations;

/// <summary>
/// Creates the index of customers' store credit.
/// </summary>
internal sealed class StoreCreditMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<StoreCreditAccountIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("CustomerId", column => column.WithLength(26))
            .Column<string>("Currency", column => column.WithLength(3))
            .Column<decimal>("Balance"),
            collection: CustomersConstants.CollectionName
        );

        // A checkout finds the buyer's credit in its currency; a customer's page lists theirs.
        await SchemaBuilder.AlterIndexTableAsync<StoreCreditAccountIndex>(table =>
        {
            table.CreateIndex("IDX_StoreCreditAccountIndex_Customer", "CustomerId", "Currency");
            table.CreateIndex("IDX_StoreCreditAccountIndex_ItemId", "ItemId");
        }, collection: CustomersConstants.CollectionName);

        return 1;
    }
}
