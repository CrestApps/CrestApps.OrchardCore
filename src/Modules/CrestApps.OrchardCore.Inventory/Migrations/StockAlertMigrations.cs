using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Models;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Inventory.Migrations;

/// <summary>
/// Creates the table of shoppers' back-in-stock requests.
/// </summary>
public sealed class StockAlertMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<StockAlertIndex>(table => table
            .Column<string>(nameof(StockAlertIndex.StockItemId), column => column.WithLength(26))
            .Column<string>(nameof(StockAlertIndex.ContentItemId), column => column.WithLength(26))
            .Column<string>(nameof(StockAlertIndex.VariantId), column => column.Nullable().WithLength(26))
            .Column<string>(nameof(StockAlertIndex.NormalizedEmail), column => column.WithLength(254))
            .Column<string>(nameof(StockAlertIndex.UserId), column => column.Nullable().WithLength(26))
            .Column<bool>(nameof(StockAlertIndex.Waiting))
            .Column<DateTime>(nameof(StockAlertIndex.CreatedUtc)),
            collection: InventoryConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<StockAlertIndex>(table =>
        {
            table.CreateIndex("IDX_StockAlertIndex_Item", nameof(StockAlertIndex.StockItemId), nameof(StockAlertIndex.Waiting));
            table.CreateIndex("IDX_StockAlertIndex_Email", nameof(StockAlertIndex.NormalizedEmail), nameof(StockAlertIndex.CreatedUtc));
            table.CreateIndex("IDX_StockAlertIndex_UserId", nameof(StockAlertIndex.UserId));
        }, collection: InventoryConstants.CollectionName);

        return 1;
    }
}
