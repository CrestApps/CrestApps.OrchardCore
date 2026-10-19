using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
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

        // Safe to run again: a server that finished the step first has already made them.
        await IndexTableIndexCreation.CreateIfMissingAsync<OrderReturnIndex>(SchemaBuilder, "IDX_OrderReturnIndex_OrderId", OrderConstants.CollectionName, nameof(OrderReturnIndex.OrderId));
        await IndexTableIndexCreation.CreateIfMissingAsync<OrderReturnIndex>(SchemaBuilder, "IDX_OrderReturnIndex_Status", OrderConstants.CollectionName, nameof(OrderReturnIndex.Status), nameof(OrderReturnIndex.CreatedUtc));

        return 1;
    }
}
