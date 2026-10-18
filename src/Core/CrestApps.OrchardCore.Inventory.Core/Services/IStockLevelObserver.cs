using CrestApps.OrchardCore.Inventory.Core.Models;

namespace CrestApps.OrchardCore.Inventory.Core.Services;

/// <summary>
/// Hears about every change to a stock item's available units while the change is being made, before it is
/// committed. An observer only notes what changed; anything that reacts to it (a notification, a workflow, a search
/// reindex) waits until the work is committed, so a change that is rolled back raises nothing.
/// </summary>
public interface IStockLevelObserver
{
    /// <summary>
    /// Notes that <paramref name="item"/> had <paramref name="previousAvailable"/> units available before this change.
    /// </summary>
    void Changed(StockItem item, int previousAvailable);
}
