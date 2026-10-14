using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Indexes;
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
            ("IDX_OrderIndex_CartId", "CartId"),
            ("IDX_OrderIndex_Status_CreatedUtc", "Status,CreatedUtc"),
        })
        {
            await SchemaBuilder.AlterIndexTableAsync<OrderIndex>(table => table
                .CreateIndex(name, column.Split(',')),
                collection: OrderConstants.CollectionName
            );
        }

        return 1;
    }
}
