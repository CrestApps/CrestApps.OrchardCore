using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Products.Core.Listing;

namespace CrestApps.OrchardCore.Inventory.Handlers;

/// <summary>
/// Refreshes a product's search record when it sells out or comes back, so a listing filtered to what is in stock
/// stays right.
/// </summary>
internal sealed class StockSearchIndexHandler : IStockLevelHandler
{
    private readonly IProductSearchIndexRefresher _refresher;

    public StockSearchIndexHandler(IProductSearchIndexRefresher refresher)
    {
        _refresher = refresher;
    }

    public Task ChangedAsync(StockLevelChangedContext context, CancellationToken cancellationToken = default)
        => context.SoldOut || context.BackInStock
            ? _refresher.RefreshAsync([context.Item.ContentItemId], cancellationToken)
            : Task.CompletedTask;
}
