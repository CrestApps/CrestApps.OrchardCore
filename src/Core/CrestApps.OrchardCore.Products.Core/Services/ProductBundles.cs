using CrestApps.OrchardCore.Products.Core.Models;

namespace CrestApps.OrchardCore.Products.Core.Services;

/// <summary>
/// What a bundle's parts can make.
/// </summary>
public static class ProductBundles
{
    /// <summary>
    /// Returns the bundle's stock from its parts': the most bundles the counted parts can make. A part whose stock
    /// isn't counted never runs out; a bundle with no counted part gets <see langword="null"/>, as an uncounted
    /// product does. It takes backorders when every counted part does.
    /// </summary>
    /// <param name="items">The bundle's parts and how many of each it holds.</param>
    /// <param name="parts">What's available of each part, by content item id.</param>
    public static ProductAvailability GetAvailability(IEnumerable<ProductBundleItem> items, IReadOnlyDictionary<string, ProductAvailability> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        int? available = null;
        int? threshold = null;
        var backorder = true;

        foreach (var item in items ?? [])
        {
            if (string.IsNullOrEmpty(item?.ContentItemId) || !parts.TryGetValue(item.ContentItemId, out var part))
            {
                continue;
            }

            var perBundle = Math.Max(1, item.Quantity);
            var makes = Math.Max(0, part.Available) / perBundle;

            available = available is null ? makes : Math.Min(available.Value, makes);
            threshold = threshold is null ? part.LowStockThreshold / perBundle : Math.Min(threshold.Value, part.LowStockThreshold / perBundle);
            backorder &= part.AllowBackorder;
        }

        return available is null ? null : new ProductAvailability(available.Value, backorder, threshold ?? 0);
    }
}
