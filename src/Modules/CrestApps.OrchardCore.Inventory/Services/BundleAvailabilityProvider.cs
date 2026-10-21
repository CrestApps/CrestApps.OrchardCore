using CrestApps.OrchardCore.Products.Core.Indexes;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using OrchardCore.ContentManagement;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Inventory.Services;

/// <summary>
/// Answers for bundles: the most of each bundle its parts in stock can make. It's asked before the stock items, so
/// a bundle's own stock item, if it has one, doesn't hide what its parts can make.
/// </summary>
public sealed class BundleAvailabilityProvider : IProductAvailabilityProvider
{
    private readonly ISession _session;
    private readonly InventoryAvailabilityProvider _inventory;

    public BundleAvailabilityProvider(ISession session, InventoryAvailabilityProvider inventory)
    {
        _session = session;
        _inventory = inventory;
    }

    public async Task<IReadOnlyDictionary<ProductStockKey, ProductAvailability>> GetAvailabilityAsync(ProductAvailabilityRequest request, CancellationToken cancellationToken = default)
    {
        var answer = new Dictionary<ProductStockKey, ProductAvailability>();
        var ids = request?.Products?
            .Where(key => key.VariantId is null && !string.IsNullOrEmpty(key.ContentItemId))
            .Select(key => key.ContentItemId)
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];

        if (ids.Length == 0)
        {
            return answer;
        }

        var rows = await _session.QueryIndex<ProductBundleIndex>(index => index.ContentItemId.IsIn(ids) && index.Published).ListAsync(cancellationToken);
        var bundles = rows.GroupBy(row => row.ContentItemId, StringComparer.Ordinal).ToArray();

        if (bundles.Length == 0)
        {
            return answer;
        }

        var partIds = rows.Select(row => row.PartContentItemId).Distinct(StringComparer.Ordinal).ToArray();
        var parts = await _inventory.GetAvailabilityAsync(new ProductAvailabilityRequest
        {
            Products = [.. partIds.Select(id => new ProductStockKey(id, null))],
            CartId = request.CartId,
        }, cancellationToken);
        var byPart = parts.Where(pair => pair.Key.VariantId is null).ToDictionary(pair => pair.Key.ContentItemId, pair => pair.Value, StringComparer.Ordinal);

        foreach (var bundle in bundles)
        {
            var items = bundle.Select(row => new ProductBundleItem { ContentItemId = row.PartContentItemId, Quantity = row.Quantity });

            if (ProductBundles.GetAvailability(items, byPart) is { } availability)
            {
                answer[new ProductStockKey(bundle.Key, null)] = availability;
            }
        }

        return answer;
    }
}
