using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Orders.Migrations;

/// <summary>
/// Creates the table returns are found by.
/// </summary>
internal sealed class OrderReturnMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<OrderReturnIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Number", column => column.WithLength(32))
            .Column<string>("OrderId", column => column.WithLength(26))
            .Column<string>("OrderNumber", column => column.Nullable().WithLength(32))
            .Column<string>("UserId", column => column.Nullable().WithLength(26))
            .Column<int>("Status")
            .Column<DateTime>("CreatedUtc"),
            collection: OrderConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<OrderReturnIndex>(table =>
        {
            table.CreateIndex("IDX_OrderReturnIndex_OrderId", "OrderId");
            table.CreateIndex("IDX_OrderReturnIndex_Status", "Status", "CreatedUtc");
        }, collection: OrderConstants.CollectionName);

        await CreateReportIndexAsync();

        return 2;
    }

    /// <summary>
    /// Adds the index reports read returns through, newest first by <c>CreatedUtc</c> a page at a time, so each page and
    /// each date range is found by seeking.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await CreateReportIndexAsync();

        return 2;
    }

    private Task<bool> CreateReportIndexAsync()
    {
        return IndexTableIndexCreation.CreateIfMissingAsync<OrderReturnIndex>(
            SchemaBuilder,
            "IDX_OrderReturnIndex_Report",
            OrderConstants.CollectionName,
            "CreatedUtc", "Id");
    }
}
