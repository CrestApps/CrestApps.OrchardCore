using CrestApps.OrchardCore.Inventory.Core.Services;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// Pushes a product to the ACP feed APIs after the units it has available change, so agents see its availability.
/// </summary>
internal sealed class AgentFeedStockHandler : IStockLevelHandler
{
    private readonly AgentFeedChangeTracker _tracker;

    public AgentFeedStockHandler(AgentFeedChangeTracker tracker)
    {
        _tracker = tracker;
    }

    public Task ChangedAsync(StockLevelChangedContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task AvailableChangedAsync(StockLevelChangedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.AvailableChanged)
        {
            _tracker.Add(context.Item.ContentItemId);
        }

        return Task.CompletedTask;
    }
}
