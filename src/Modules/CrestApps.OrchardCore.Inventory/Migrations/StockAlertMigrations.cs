using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
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

        await IndexTableIndexCreation.CreateIfMissingAsync<StockAlertIndex>(SchemaBuilder, "IDX_StockAlertIndex_Item", InventoryConstants.CollectionName, nameof(StockAlertIndex.StockItemId), nameof(StockAlertIndex.Waiting));
        await IndexTableIndexCreation.CreateIfMissingAsync<StockAlertIndex>(SchemaBuilder, "IDX_StockAlertIndex_Email", InventoryConstants.CollectionName, nameof(StockAlertIndex.NormalizedEmail), nameof(StockAlertIndex.CreatedUtc));
        await IndexTableIndexCreation.CreateIfMissingAsync<StockAlertIndex>(SchemaBuilder, "IDX_StockAlertIndex_UserId", InventoryConstants.CollectionName, nameof(StockAlertIndex.UserId));

        return 2;
    }

    /// <summary>
    /// Adds who asked, so a signed-in shopper's requests are found for their data requests.
    /// </summary>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<StockAlertIndex>(table => table
            .AddColumn<string>(nameof(StockAlertIndex.UserId), column => column.Nullable().WithLength(26)),
            collection: InventoryConstants.CollectionName);

        await IndexTableIndexCreation.CreateIfMissingAsync<StockAlertIndex>(SchemaBuilder, "IDX_StockAlertIndex_UserId", InventoryConstants.CollectionName, nameof(StockAlertIndex.UserId));

        return 2;
    }
}
