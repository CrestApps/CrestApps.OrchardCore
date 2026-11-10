using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Products.Core.Feeds;
using OrchardCore.Environment.Cache;

namespace CrestApps.OrchardCore.Inventory.Handlers;

/// <summary>
/// Drops the product feeds the tenant keeps when the units a product has available change, since feeds show its
/// availability and the units left; the next read builds them again.
/// </summary>
internal sealed class ProductFeedStockHandler : IStockLevelHandler
{
    private readonly ISignal _signal;

    public ProductFeedStockHandler(ISignal signal)
    {
        _signal = signal;
    }

    public Task ChangedAsync(StockLevelChangedContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task AvailableChangedAsync(StockLevelChangedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.AvailableChanged)
        {
            await _signal.SignalTokenAsync(ProductFeedCache.SignalKey);
        }
    }
}
