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

    // Credit can belong to a party other than a customer record, such as a business account. Existing accounts keep
    // their customer and leave the new columns empty, so no stored balance moves.
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<StoreCreditAccountIndex>(table =>
        {
            table.AddColumn<string>("PartyKind", column => column.Nullable().WithLength(64));
            table.AddColumn<string>("PartyId", column => column.Nullable().WithLength(64));
        }, collection: CustomersConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<StoreCreditAccountIndex>(table =>
            table.CreateIndex("IDX_StoreCreditAccountIndex_Party", "PartyKind", "PartyId", "Currency"),
            collection: CustomersConstants.CollectionName);

        return 2;
    }
}
