using CrestApps.OrchardCore.Inventory.Core.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Inventory.Services;

/// <summary>
/// Tells the <see cref="IStockLevelHandler"/>s about committed changes to the units stock items have available and to
/// their levels.
/// </summary>
/// <remarks>
/// The items are read again, as committed. A change that was rolled back, such as a checkout that lost a race for
/// the same stock, left the item where it was, so it raises nothing.
/// </remarks>
internal sealed class StockLevelDispatcher
{
    private readonly IStockItemStore _items;
    private readonly IEnumerable<IStockLevelHandler> _handlers;
    private readonly ILogger _logger;

    public StockLevelDispatcher(IStockItemStore items, IEnumerable<IStockLevelHandler> handlers, ILogger<StockLevelDispatcher> logger)
    {
        _items = items;
        _handlers = handlers;
        _logger = logger;
    }

    public async Task DispatchAsync(IReadOnlyDictionary<string, int> previousAvailable, CancellationToken cancellationToken = default)
    {
        if (!_handlers.Any())
        {
            return;
        }

        foreach (var (stockItemId, previous) in previousAvailable)
        {
            var item = await _items.FindByIdAsync(stockItemId, cancellationToken);

            if (item is null || item.Archived)
            {
                continue;
            }

            var context = new StockLevelChangedContext(item, previous);

            if (context.AvailableChanged)
            {
                await InvokeAsync(context, static (handler, change, token) => handler.AvailableChangedAsync(change, token), cancellationToken);
            }

            if (context.Level != context.PreviousLevel)
            {
                await InvokeAsync(context, static (handler, change, token) => handler.ChangedAsync(change, token), cancellationToken);
            }
        }
    }

    private async Task InvokeAsync(
        StockLevelChangedContext context,
        Func<IStockLevelHandler, StockLevelChangedContext, CancellationToken, Task> invoke,
        CancellationToken cancellationToken)
    {
        foreach (var handler in _handlers)
        {
            try
            {
                await invoke(handler, context, cancellationToken);
            }
            catch (Exception ex)
            {
                // A failed notification must not keep listings, workflows or other handlers from hearing of it.
                _logger.LogError(ex, "The stock level handler '{Handler}' failed for the stock item '{StockItemId}'.", handler.GetType().FullName, context.Item.ItemId);
            }
        }
    }
}
