using CrestApps.Core;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Products.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Inventory.Core.Services;

/// <summary>
/// Keeps a product's stock items in step with the product: one per variant, or one for a product without variants,
/// with the product's SKU, title and stock policy. Stock items of removed variants, and of products no longer tracked,
/// are archived, never deleted, so their history stays.
/// </summary>
public sealed class InventoryProductSynchronizer
{
    private readonly IStockItemStore _items;
    private readonly IClock _clock;

    public InventoryProductSynchronizer(IStockItemStore items, IClock clock)
    {
        _items = items;
        _clock = clock;
    }

    /// <summary>
    /// Brings the product's stock items up to date with it, or archives them all when the product is gone
    /// (<paramref name="product"/> is <see langword="null"/>) or its type no longer has the Inventory part. Returns
    /// whether a stock item was added or archived, which changes what the product has in stock.
    /// </summary>
    public async Task<bool> SyncAsync(string contentItemId, ContentItem product, bool typeHasInventory, InventorySettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(contentItemId);

        var existing = await _items.ListByProductAsync(contentItemId, cancellationToken);
        var wanted = GetWanted(typeHasInventory ? product : null, settings);
        var now = _clock.UtcNow;
        var counted = false;

        foreach (var item in existing)
        {
            if (wanted.Remove(item.VariantId ?? string.Empty, out var target))
            {
                counted |= item.Archived;

                if (Apply(item, target, archived: false))
                {
                    item.ModifiedUtc = now;
                    await _items.UpdateAsync(item, cancellationToken);
                }
            }
            else if (!item.Archived)
            {
                counted = true;
                item.Archived = true;
                item.ModifiedUtc = now;
                await _items.UpdateAsync(item, cancellationToken);
            }
        }

        foreach (var (variantKey, target) in wanted)
        {
            var item = new StockItem
            {
                ItemId = UniqueId.GenerateId(),
                ContentItemId = contentItemId,
                VariantId = variantKey.Length == 0 ? null : variantKey,
                CreatedUtc = now,
                ModifiedUtc = now,
            };

            Apply(item, target, archived: false);

            await _items.CreateAsync(item, cancellationToken);
            counted = true;
        }

        return counted;
    }

    // What the product wants counted, keyed by variant id (empty for a product without variants).
    internal static Dictionary<string, StockItem> GetWanted(ContentItem product, InventorySettings settings)
    {
        var wanted = new Dictionary<string, StockItem>(StringComparer.Ordinal);

        // A product saved before its type had the part has no part data yet, and is counted with the defaults.
        if (product is null)
        {
            return wanted;
        }

        var inventory = product.TryGet<InventoryPart>(out var part) ? part : new InventoryPart();

        if (!inventory.TrackInventory)
        {
            return wanted;
        }

        var threshold = Math.Max(0, inventory.LowStockThreshold ?? settings?.DefaultLowStockThreshold ?? 0);
        var productSku = product.TryGet<ProductPart>(out var productPart) ? productPart.Sku : null;

        if (product.TryGet<ProductVariantsPart>(out var variants) && variants.HasVariants)
        {
            var optionNames = variants.Options?.Count > 0 ? variants.Options.Select(option => option.Name).ToArray() : null;

            foreach (var variant in variants.Variants.Where(variant => !string.IsNullOrEmpty(variant.VariantId)))
            {
                var variantTitle = variant.GetTitle(optionNames);

                wanted[variant.VariantId] = new StockItem
                {
                    Sku = string.IsNullOrWhiteSpace(variant.Sku) ? productSku : variant.Sku,
                    Title = string.IsNullOrEmpty(variantTitle) ? product.DisplayText : $"{product.DisplayText} ({variantTitle})",
                    LowStockThreshold = threshold,
                    AllowBackorder = inventory.AllowBackorder,
                    ReleaseDateUtc = inventory.ReleaseDateUtc,
                };
            }
        }
        else
        {
            wanted[string.Empty] = new StockItem
            {
                Sku = productSku,
                Title = product.DisplayText,
                LowStockThreshold = threshold,
                AllowBackorder = inventory.AllowBackorder,
                ReleaseDateUtc = inventory.ReleaseDateUtc,
            };
        }

        return wanted;
    }

    private static bool Apply(StockItem item, StockItem target, bool archived)
    {
        var changed = item.Sku != target.Sku ||
            item.Title != target.Title ||
            item.LowStockThreshold != target.LowStockThreshold ||
            item.AllowBackorder != target.AllowBackorder ||
            item.ReleaseDateUtc != target.ReleaseDateUtc ||
            item.Archived != archived;

        item.Sku = target.Sku;
        item.Title = target.Title;
        item.LowStockThreshold = target.LowStockThreshold;
        item.AllowBackorder = target.AllowBackorder;
        item.ReleaseDateUtc = target.ReleaseDateUtc;
        item.Archived = archived;

        return changed;
    }
}
