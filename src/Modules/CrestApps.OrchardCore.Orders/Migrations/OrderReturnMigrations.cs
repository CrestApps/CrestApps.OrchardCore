using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Orders.Migrations;

/// <summary>
/// Creates the table returns are found by.
/// </summary>
public sealed class OrderReturnMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<OrderReturnIndex>(table => table
            .Column<string>(nameof(OrderReturnIndex.ItemId), column => column.WithLength(26))
            .Column<string>(nameof(OrderReturnIndex.Number), column => column.WithLength(32))
            .Column<string>(nameof(OrderReturnIndex.OrderId), column => column.WithLength(26))
            .Column<string>(nameof(OrderReturnIndex.OrderNumber), column => column.Nullable().WithLength(32))
            .Column<string>(nameof(OrderReturnIndex.UserId), column => column.Nullable().WithLength(26))
            .Column<int>(nameof(OrderReturnIndex.Status))
            .Column<DateTime>(nameof(OrderReturnIndex.CreatedUtc)),
            collection: OrderConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<OrderReturnIndex>(table =>
        {
            table.CreateIndex("IDX_OrderReturnIndex_OrderId", nameof(OrderReturnIndex.OrderId));
            table.CreateIndex("IDX_OrderReturnIndex_Status", nameof(OrderReturnIndex.Status), nameof(OrderReturnIndex.CreatedUtc));
        }, collection: OrderConstants.CollectionName);

        return 1;
    }
}
