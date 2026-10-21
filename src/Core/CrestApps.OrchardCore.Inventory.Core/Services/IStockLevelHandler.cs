using CrestApps.OrchardCore.Inventory.Core.Models;

namespace CrestApps.OrchardCore.Inventory.Core.Services;

/// <summary>
/// Reacts once a change to a stock item is committed. Handlers run after the request or background job that made the
/// change, in a unit of work of their own.
/// </summary>
public interface IStockLevelHandler
{
    /// <summary>
    /// Reacts to a change of the item's level: it ran low, sold out or came back.
    /// </summary>
    /// <param name="context">The committed change.</param>
    /// <param name="cancellationToken">A token that cancels the work.</param>
    Task ChangedAsync(StockLevelChangedContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reacts to any change of the units the item has available, whether or not its level changed. Several changes in
    /// one unit of work are one, from the units before the first to the units after the last.
    /// </summary>
    /// <param name="context">The committed change.</param>
    /// <param name="cancellationToken">A token that cancels the work.</param>
    Task AvailableChangedAsync(StockLevelChangedContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
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
    /// Gets whether the units available changed.
    /// </summary>
    public bool AvailableChanged
        => Item.Available != PreviousAvailable;

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
