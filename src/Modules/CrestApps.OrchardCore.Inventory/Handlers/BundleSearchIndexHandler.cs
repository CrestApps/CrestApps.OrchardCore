using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Products.Core.Indexes;
using CrestApps.OrchardCore.Products.Core.Listing;
using YesSql;

namespace CrestApps.OrchardCore.Inventory.Handlers;

/// <summary>
/// Refreshes the search records of the bundles a product is part of when it sells out or comes back, so a listing
/// filtered to what's in stock stays right for them too.
/// </summary>
internal sealed class BundleSearchIndexHandler : IStockLevelHandler
{
    private readonly ISession _session;
    private readonly IProductSearchIndexRefresher _refresher;

    public BundleSearchIndexHandler(ISession session, IProductSearchIndexRefresher refresher)
    {
        _session = session;
        _refresher = refresher;
    }

    public async Task ChangedAsync(StockLevelChangedContext context, CancellationToken cancellationToken = default)
    {
        if (!context.SoldOut && !context.BackInStock)
        {
            return;
        }

        var partId = context.Item.ContentItemId;
        var bundleIds = (await _session.QueryIndex<ProductBundleIndex>(index => index.PartContentItemId == partId && index.Published).ListAsync(cancellationToken))
            .Select(row => row.ContentItemId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (bundleIds.Length > 0)
        {
            await _refresher.RefreshAsync(bundleIds, cancellationToken);
        }
    }
}
