using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Services;
using CrestApps.OrchardCore.Products.Core.Transfer;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Inventory.Handlers;

/// <summary>
/// Counts the stock a product import says is available, for the products and variants whose stock is counted. The stock items are
/// made first, as they would be after the products were saved.
/// </summary>
internal sealed class InventoryProductImportHandler : IProductImportHandler
{
    private readonly InventorySyncService _sync;
    private readonly IStockItemStore _stockItems;
    private readonly IInventoryService _inventory;

    internal readonly IStringLocalizer S;

    public InventoryProductImportHandler(
        InventorySyncService sync,
        IStockItemStore stockItems,
        IInventoryService inventory,
        IStringLocalizer<InventoryProductImportHandler> stringLocalizer)
    {
        _sync = sync;
        _stockItems = stockItems;
        _inventory = inventory;
        S = stringLocalizer;
    }

    public async Task ImportedAsync(IReadOnlyList<ProductImportEntry> entries, CancellationToken cancellationToken = default)
    {
        await _sync.SyncAsync(entries.Select(entry => entry.ContentItem.ContentItemId), cancellationToken);

        foreach (var (item, product) in entries)
        {
            var counts = product.Variants.Count > 0
                ? product.Variants.Where(variant => variant.Quantity.HasValue && variant.VariantId is not null).Select(variant => (variant.VariantId, variant.Quantity.Value))
                : product.Quantity is { } quantity ? [((string)null, quantity)] : [];

            foreach (var (variantId, count) in counts)
            {
                // The file says what's available to sell, so the units unpaid orders hold stay on hand besides it.
                if (await _stockItems.FindAsync(item.ContentItemId, variantId, cancellationToken: cancellationToken) is { } stockItem &&
                    stockItem.Available != Math.Max(0, count))
                {
                    await _inventory.SetCountAsync(stockItem.ItemId, Math.Max(0, count) + Math.Max(0, stockItem.Reserved), S["Imported"], actor: null, cancellationToken);
                }
            }
        }
    }
}
