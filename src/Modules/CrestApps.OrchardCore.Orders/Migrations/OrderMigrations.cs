using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Orders.Migrations;

/// <summary>
/// Creates the index table that finds orders.
/// </summary>
public sealed class OrderMigrations : DataMigration
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
            .Column<DateTime>("AnonymizedUtc", column => column.Nullable()),
            collection: OrderConstants.CollectionName
        );

        foreach (var (name, column) in new[]
        {
            ("IDX_OrderIndex_ItemId", "ItemId"),
            ("IDX_OrderIndex_OrderNumber", "OrderNumber"),
            ("IDX_OrderIndex_UserId", "UserId"),
            ("IDX_OrderIndex_CustomerId", "CustomerId"),
            ("IDX_OrderIndex_CustomerEmail", "CustomerEmail"),
            ("IDX_OrderIndex_CartId", "CartId"),
            ("IDX_OrderIndex_Status_CreatedUtc", "Status,CreatedUtc"),
        })
        {
            await SchemaBuilder.AlterIndexTableAsync<OrderIndex>(table => table
                .CreateIndex(name, column.Split(',')),
                collection: OrderConstants.CollectionName
            );
        }

        return 4;
    }

    /// <summary>
    /// Records the customer who placed each order.
    /// </summary>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<OrderIndex>(table => table
            .AddColumn<string>("CustomerId", column => column.WithLength(26)),
            collection: OrderConstants.CollectionName
        );

        // Idempotent, because a scaled-out site can run an upgrade step twice.
        await IndexTableIndexCreation.CreateIfMissingAsync<OrderIndex>(SchemaBuilder, "IDX_OrderIndex_CustomerId", OrderConstants.CollectionName, "CustomerId");

        return 2;
    }

    /// <summary>
    /// Finds orders by the customer's email, for Find my order.
    /// </summary>
    public async Task<int> UpdateFrom2Async()
    {
        await IndexTableIndexCreation.CreateIfMissingAsync<OrderIndex>(SchemaBuilder, "IDX_OrderIndex_CustomerEmail", OrderConstants.CollectionName, "CustomerEmail");

        return 3;
    }

    /// <summary>
    /// Records when an order's personal details were removed, so the retention task does not visit it again.
    /// </summary>
    public async Task<int> UpdateFrom3Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<OrderIndex>(table => table
            .AddColumn<DateTime>("AnonymizedUtc", column => column.Nullable()),
            collection: OrderConstants.CollectionName
        );

        return 4;
    }

    /// <summary>
    /// Records where each order was made, the location it's sold from and the register shift that rang it up, for reports
    /// by channel and store and for a shift's count. Orders made before have no channel and count as online.
    /// </summary>
    public async Task<int> UpdateFrom4Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<OrderIndex>(table => table
            .AddColumn<string>("Channel", column => column.Nullable().WithLength(16)),
            collection: OrderConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<OrderIndex>(table => table
            .AddColumn<string>("LocationId", column => column.Nullable().WithLength(26)),
            collection: OrderConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<OrderIndex>(table => table
            .AddColumn<string>("ShiftId", column => column.Nullable().WithLength(26)),
            collection: OrderConstants.CollectionName
        );

        await IndexTableIndexCreation.CreateIfMissingAsync<OrderIndex>(SchemaBuilder, "IDX_OrderIndex_ShiftId", OrderConstants.CollectionName, "ShiftId");

        await IndexTableIndexCreation.CreateIfMissingAsync<OrderIndex>(SchemaBuilder, "IDX_OrderIndex_Channel", OrderConstants.CollectionName, "Channel", "LocationId");

        return 5;
    }

    /// <summary>
    /// Records the company an order was placed for and the buyer's purchase order number, so a company's orders can be
    /// listed and an order found by its PO number.
    /// </summary>
    public async Task<int> UpdateFrom5Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<OrderIndex>(table => table
            .AddColumn<string>("CompanyId", column => column.Nullable().WithLength(26)),
            collection: OrderConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<OrderIndex>(table => table
            .AddColumn<string>("PurchaseOrderNumber", column => column.Nullable().WithLength(64)),
            collection: OrderConstants.CollectionName
        );

        await IndexTableIndexCreation.CreateIfMissingAsync<OrderIndex>(SchemaBuilder, "IDX_OrderIndex_CompanyId", OrderConstants.CollectionName, "CompanyId");

        await IndexTableIndexCreation.CreateIfMissingAsync<OrderIndex>(SchemaBuilder, "IDX_OrderIndex_PurchaseOrder", OrderConstants.CollectionName, "PurchaseOrderNumber");

        return 6;
    }
}
