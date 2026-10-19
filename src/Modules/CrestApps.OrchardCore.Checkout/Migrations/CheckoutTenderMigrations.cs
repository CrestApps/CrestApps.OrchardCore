using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Checkout.Migrations;

/// <summary>
/// Creates the ledger of what balances, such as gift cards, pay toward checkouts.
/// </summary>
public sealed class CheckoutTenderMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CheckoutTenderHoldIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("SessionId", column => column.WithLength(26))
            .Column<string>("ProviderKey", column => column.WithLength(64))
            .Column<string>("Reference", column => column.WithLength(64))
            .Column<string>("Status", column => column.WithLength(16))
        );

        // Every rebuilt invoice counts the holds on a balance; completing and refunding find a checkout's.
        await IndexTableIndexCreation.CreateIfMissingAsync<CheckoutTenderHoldIndex>(SchemaBuilder, "IDX_CheckoutTenderHoldIndex_Balance", null, "ProviderKey", "Reference", "Status");
        await IndexTableIndexCreation.CreateIfMissingAsync<CheckoutTenderHoldIndex>(SchemaBuilder, "IDX_CheckoutTenderHoldIndex_Session", null, "SessionId", "Status");

        return 1;
    }
}
