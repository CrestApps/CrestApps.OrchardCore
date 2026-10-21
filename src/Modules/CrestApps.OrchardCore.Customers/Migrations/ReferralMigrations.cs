using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Customers.Migrations;

/// <summary>
/// Creates the index of customers' referral links.
/// </summary>
internal sealed class ReferralMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ReferralProfileIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("CustomerId", column => column.WithLength(26))
            .Column<string>("Code", column => column.WithLength(16)),
            collection: CustomersConstants.CollectionName
        );

        // A link is opened by its code; a customer's account finds theirs.
        await SchemaBuilder.AlterIndexTableAsync<ReferralProfileIndex>(table =>
        {
            table.CreateIndex("IDX_ReferralProfileIndex_Code", "Code");
            table.CreateIndex("IDX_ReferralProfileIndex_Customer", "CustomerId");
        }, collection: CustomersConstants.CollectionName);

        return 1;
    }
}
