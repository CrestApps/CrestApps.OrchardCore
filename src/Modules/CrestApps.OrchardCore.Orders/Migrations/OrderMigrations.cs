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
            .Column<DateTime>("CreatedUtc"),
            collection: OrderConstants.CollectionName
        );

        foreach (var (name, column) in new[]
        {
            ("IDX_OrderIndex_ItemId", "ItemId"),
            ("IDX_OrderIndex_OrderNumber", "OrderNumber"),
            ("IDX_OrderIndex_UserId", "UserId"),
            ("IDX_OrderIndex_CustomerId", "CustomerId"),
            ("IDX_OrderIndex_CartId", "CartId"),
            ("IDX_OrderIndex_Status_CreatedUtc", "Status,CreatedUtc"),
        })
        {
            await SchemaBuilder.AlterIndexTableAsync<OrderIndex>(table => table
                .CreateIndex(name, column.Split(',')),
                collection: OrderConstants.CollectionName
            );
        }

        return 2;
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
}
