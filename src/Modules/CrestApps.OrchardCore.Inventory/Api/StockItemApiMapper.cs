using CrestApps.OrchardCore.Inventory.Core.Models;

namespace CrestApps.OrchardCore.Inventory.Api;

/// <summary>
/// Writes a stock item the way the inventory API and the stock webhooks show it.
/// </summary>
internal static class StockItemApiMapper
{
    public static ApiStockItem ToApi(StockItem item)
        => new(
            item.ItemId,
            item.ContentItemId,
            item.VariantId,
            item.Sku,
            item.Title,
            item.OnHand,
            item.Reserved,
            item.Available,
            item.LowStockThreshold,
            item.AllowBackorder,
            item.Archived,
            (item.Levels ?? []).Select(level => new ApiStockLevel(level.LocationId, level.OnHand, level.Reserved, level.Available)).ToList());
}
