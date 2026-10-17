using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Inventory.Services;

/// <summary>
/// Notes the stock items whose available units change during a request or a background job, and once it is over,
/// has <see cref="StockLevelDispatcher"/> tell the <see cref="IStockLevelHandler"/>s what changed.
/// </summary>
internal sealed class StockLevelTracker : IStockLevelObserver
{
    private Dictionary<string, int> _previous;

    public void Changed(StockItem item, int previousAvailable)
    {
        if (string.IsNullOrEmpty(item?.ItemId))
        {
            return;
        }

        if (_previous is null)
        {
            var previous = _previous = new Dictionary<string, int>(StringComparer.Ordinal);

            ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<StockLevelDispatcher>().DispatchAsync(previous));
        }

        // The level before the first change is the one that counts: several changes in one unit of work are one.
        _previous.TryAdd(item.ItemId, previousAvailable);
    }
}
