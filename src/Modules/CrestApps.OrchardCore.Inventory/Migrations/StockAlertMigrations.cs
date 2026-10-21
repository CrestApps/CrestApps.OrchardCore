using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Models;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Inventory.Migrations;

/// <summary>
/// Creates the table of shoppers' back-in-stock requests.
/// </summary>
internal sealed class StockAlertMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<StockAlertIndex>(table => table
            .Column<string>("StockItemId", column => column.WithLength(26))
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("VariantId", column => column.Nullable().WithLength(26))
            .Column<string>("NormalizedEmail", column => column.WithLength(254))
            .Column<string>("UserId", column => column.Nullable().WithLength(26))
            .Column<bool>("Waiting")
            .Column<DateTime>("CreatedUtc"),
            collection: InventoryConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<StockAlertIndex>(table =>
        {
            table.CreateIndex("IDX_StockAlertIndex_Item", "StockItemId", "Waiting");
            table.CreateIndex("IDX_StockAlertIndex_Email", "NormalizedEmail", "CreatedUtc");
            table.CreateIndex("IDX_StockAlertIndex_UserId", "UserId");
        }, collection: InventoryConstants.CollectionName);

        return 1;
    }
}
