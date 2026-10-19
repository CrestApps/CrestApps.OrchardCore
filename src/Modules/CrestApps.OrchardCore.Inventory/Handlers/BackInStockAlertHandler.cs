using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Services;

namespace CrestApps.OrchardCore.Inventory.Handlers;

/// <summary>
/// Emails the shoppers waiting for a stock item when it comes back.
/// </summary>
internal sealed class BackInStockAlertHandler : IStockLevelHandler
{
    private readonly StockAlertService _alerts;

    public BackInStockAlertHandler(StockAlertService alerts)
    {
        _alerts = alerts;
    }

    public async Task ChangedAsync(StockLevelChangedContext context, CancellationToken cancellationToken = default)
    {
        if (context.BackInStock)
        {
            await _alerts.NotifyAsync(context.Item, cancellationToken);
        }
    }
}
