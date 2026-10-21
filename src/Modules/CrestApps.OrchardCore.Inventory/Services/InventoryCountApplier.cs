using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Products.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Inventory.Services;

/// <summary>
/// Sets how many of a product, or of each of its variants, are available to sell, as an import says. The units unpaid
/// orders hold stay on hand besides them, and an item already at the count is left alone.
/// </summary>
public sealed class InventoryCountApplier
{
    private readonly IContentManager _contentManager;
    private readonly IStockItemStore _stockItems;
    private readonly IInventoryService _inventory;

    internal readonly IStringLocalizer S;

    public InventoryCountApplier(
        IContentManager contentManager,
        IStockItemStore stockItems,
        IInventoryService inventory,
        IStringLocalizer<InventoryCountApplier> stringLocalizer)
    {
        _contentManager = contentManager;
        _stockItems = stockItems;
        _inventory = inventory;
        S = stringLocalizer;
    }

    /// <param name="contentItemId">The product.</param>
    /// <param name="stock">The count of a product without variants, or <see langword="null"/>.</param>
    /// <param name="variantStock">Each variant's count, by its SKU or its options, such as "M / Red".</param>
    public async Task ApplyAsync(string contentItemId, int? stock, IReadOnlyDictionary<string, int> variantStock)
    {
        var counts = new List<(string VariantId, int Count)>();

        if (variantStock is { Count: > 0 })
        {
            var product = await _contentManager.GetAsync(contentItemId, VersionOptions.Latest);

            if (product?.TryGet<ProductVariantsPart>(out var variants) == true)
            {
                var optionNames = variants.Options?.Select(option => option.Name).ToArray();

                foreach (var variant in variants.Variants)
                {
                    if ((!string.IsNullOrEmpty(variant.Sku) && variantStock.TryGetValue(variant.Sku, out var count)) ||
                        variantStock.TryGetValue(variant.GetTitle(optionNames), out count))
                    {
                        counts.Add((variant.VariantId, count));
                    }
                }
            }
        }
        else if (stock.HasValue)
        {
            counts.Add((null, stock.Value));
        }

        foreach (var (variantId, count) in counts)
        {
            if (await _stockItems.FindAsync(contentItemId, variantId) is { } item && item.Available != count)
            {
                await _inventory.SetCountAsync(item.ItemId, count + Math.Max(0, item.Reserved), S["Imported"], actor: null);
            }
        }
    }
}
