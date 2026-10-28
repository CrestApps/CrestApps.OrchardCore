using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Core.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Customers.Migrations;

internal sealed class CustomerMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CustomerIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("NormalizedEmail", column => column.WithLength(255))
            .Column<string>("NormalizedName", column => column.WithLength(255))
            .Column<string>("NormalizedCompany", column => column.WithLength(255))
            .Column<int>("Kind")
            .Column<int>("Status")
            .Column<bool>("HasAccount")
            .Column<DateTime>("CreatedUtc")
            .Column<DateTime>("LastActivityUtc"),
            collection: CustomersConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CustomerIndex>(table =>
        {
            table.CreateIndex("IDX_CustomerIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_CustomerIndex_UserId", "UserId");
            table.CreateIndex("IDX_CustomerIndex_NormalizedEmail", "NormalizedEmail");
            table.CreateIndex("IDX_CustomerIndex_NormalizedName", "NormalizedName");
            table.CreateIndex("IDX_CustomerIndex_Status_LastActivityUtc", "Status", "LastActivityUtc");
        }, collection: CustomersConstants.CollectionName);

        await CreateReportIndexAsync();

        return 2;
    }

    /// <summary>
    /// Adds the index reports read customers through, newest first by <c>CreatedUtc</c> a page at a time, so each page
    /// and each date range is found by seeking.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await CreateReportIndexAsync();

        return 2;
    }

    private Task<bool> CreateReportIndexAsync()
    {
        return IndexTableIndexCreation.CreateIfMissingAsync<CustomerIndex>(
            SchemaBuilder,
            "IDX_CustomerIndex_Report",
            CustomersConstants.CollectionName,
            "CreatedUtc", "Id");
    }
}
