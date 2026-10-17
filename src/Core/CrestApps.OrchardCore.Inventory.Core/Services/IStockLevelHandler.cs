using CrestApps.OrchardCore.Inventory.Core.Models;

namespace CrestApps.OrchardCore.Inventory.Core.Services;

/// <summary>
/// Reacts once a change to a stock item's level is committed: it ran low, sold out or came back. Handlers run after
/// the request or background job that made the change, in a unit of work of their own.
/// </summary>
public interface IStockLevelHandler
{
    Task ChangedAsync(StockLevelChangedContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// A committed change to a stock item's level.
/// </summary>
public sealed class StockLevelChangedContext
{
    public StockLevelChangedContext(StockItem item, int previousAvailable)
    {
        ArgumentNullException.ThrowIfNull(item);

        Item = item;
        PreviousAvailable = previousAvailable;
        PreviousLevel = StockLevels.Of(previousAvailable, item.LowStockThreshold);
        Level = StockLevels.Of(item.Available, item.LowStockThreshold);
    }

    /// <summary>
    /// Gets the stock item as committed.
    /// </summary>
    public StockItem Item { get; }

    /// <summary>
    /// Gets the units that were available before the first change in the unit of work.
    /// </summary>
    public int PreviousAvailable { get; }

    public StockLevel PreviousLevel { get; }

    public StockLevel Level { get; }

    /// <summary>
    /// Gets whether the item dropped to low or out of stock: the moment to reorder.
    /// </summary>
    public bool RanLow
        => Level > PreviousLevel;

    /// <summary>
    /// Gets whether nothing is left, when there was before.
    /// </summary>
    public bool SoldOut
        => Level == StockLevel.Out && PreviousLevel != StockLevel.Out;

    /// <summary>
    /// Gets whether units are available again after none were.
    /// </summary>
    public bool BackInStock
        => Level != StockLevel.Out && PreviousLevel == StockLevel.Out;
}
