using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Orders.Migrations;

/// <summary>
/// Creates the index table that finds orders.
/// </summary>
internal sealed class OrderMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<OrderIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("OrderNumber", column => column.WithLength(20))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("CustomerId", column => column.WithLength(26))
            .Column<string>("CustomerEmail", column => column.WithLength(255))
            .Column<string>("CartId", column => column.WithLength(26))
            .Column<string>("CheckoutSessionId", column => column.WithLength(26))
            .Column<int>("Status")
            .Column<DateTime>("CreatedUtc")
            .Column<DateTime>("AnonymizedUtc", column => column.Nullable())
            .Column<string>("Channel", column => column.Nullable().WithLength(16))
            .Column<string>("LocationId", column => column.Nullable().WithLength(26))
            .Column<string>("ShiftId", column => column.Nullable().WithLength(26))
            .Column<string>("CompanyId", column => column.Nullable().WithLength(26))
            .Column<string>("PurchaseOrderNumber", column => column.Nullable().WithLength(64)),
            collection: OrderConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<OrderIndex>(table =>
        {
            table.CreateIndex("IDX_OrderIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_OrderIndex_OrderNumber", "OrderNumber");
            table.CreateIndex("IDX_OrderIndex_UserId", "UserId");
            table.CreateIndex("IDX_OrderIndex_CustomerId", "CustomerId");
            table.CreateIndex("IDX_OrderIndex_CustomerEmail", "CustomerEmail");
            table.CreateIndex("IDX_OrderIndex_CartId", "CartId");
            table.CreateIndex("IDX_OrderIndex_Status_CreatedUtc", "Status", "CreatedUtc");
            table.CreateIndex("IDX_OrderIndex_ShiftId", "ShiftId");
            table.CreateIndex("IDX_OrderIndex_Channel", "Channel", "LocationId");
            table.CreateIndex("IDX_OrderIndex_CompanyId", "CompanyId");
            table.CreateIndex("IDX_OrderIndex_PurchaseOrder", "PurchaseOrderNumber");
        }, collection: OrderConstants.CollectionName);

        await CreateReportIndexAsync();

        return 2;
    }

    /// <summary>
    /// Adds the index reports read orders through, newest first by <c>CreatedUtc</c> a page at a time, so each page and
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
        return IndexTableIndexCreation.CreateIfMissingAsync<OrderIndex>(
            SchemaBuilder,
            "IDX_OrderIndex_Report",
            OrderConstants.CollectionName,
            "CreatedUtc", "Id");
    }
}
