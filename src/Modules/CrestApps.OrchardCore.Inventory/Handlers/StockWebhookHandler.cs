using CrestApps.OrchardCore.Commerce.Webhooks;
using CrestApps.OrchardCore.Inventory.Api;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;

namespace CrestApps.OrchardCore.Inventory.Handlers;

/// <summary>
/// Tells webhooks when stock runs low or sells out. Handlers hear only of changes of level (in stock, low, out).
/// </summary>
internal sealed class StockWebhookHandler : IStockLevelHandler
{
    private readonly ICommerceWebhookPublisher _publisher;

    public StockWebhookHandler(ICommerceWebhookPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task ChangedAsync(StockLevelChangedContext context, CancellationToken cancellationToken = default)
    {
        var data = new { stock = InventoryAdminApiEndpoints.ToApi(context.Item), previousAvailable = context.PreviousAvailable };

        if (context.SoldOut)
        {
            await _publisher.PublishAsync(CommerceWebhookEvents.StockSoldOut, data, cancellationToken);
        }
        else if (context.RanLow)
        {
            await _publisher.PublishAsync(CommerceWebhookEvents.StockLow, data, cancellationToken);
        }
    }
}
