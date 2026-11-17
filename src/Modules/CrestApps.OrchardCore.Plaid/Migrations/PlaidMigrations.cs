using CrestApps.OrchardCore.Plaid.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Plaid.Migrations;

/// <summary>
/// Creates the index of kept Plaid items.
/// </summary>
internal sealed class PlaidMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<PlaidItemIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(100))
            .Column<string>("BankAccountId", column => column.WithLength(26)),
            collection: PlaidConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<PlaidItemIndex>(table => table
            .CreateIndex("IDX_PlaidItemIndex_ItemId", "ItemId", "DocumentId"),
            collection: PlaidConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<PlaidItemIndex>(table => table
            .CreateIndex("IDX_PlaidItemIndex_BankAccountId", "BankAccountId", "DocumentId"),
            collection: PlaidConstants.CollectionName);

        return 1;
    }
}
