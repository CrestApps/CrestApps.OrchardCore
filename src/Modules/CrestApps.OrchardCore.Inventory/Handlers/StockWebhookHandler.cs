using CrestApps.OrchardCore.Commerce.Webhooks;
using CrestApps.OrchardCore.Inventory.Api;
using CrestApps.OrchardCore.Inventory.Core.Services;

namespace CrestApps.OrchardCore.Inventory.Handlers;

/// <summary>
/// Tells webhooks of every change to what's available, so another system (a marketplace listing, a warehouse) keeps
/// the same count, and of stock running low or selling out.
/// </summary>
internal sealed class StockWebhookHandler : IStockLevelHandler
{
    private readonly ICommerceWebhookPublisher _publisher;

    public StockWebhookHandler(ICommerceWebhookPublisher publisher)
    {
        _publisher = publisher;
    }

    public Task AvailableChangedAsync(StockLevelChangedContext context, CancellationToken cancellationToken = default)
        => _publisher.PublishAsync(CommerceWebhookEvents.StockChanged, ToData(context), cancellationToken);

    public async Task ChangedAsync(StockLevelChangedContext context, CancellationToken cancellationToken = default)
    {
        if (context.SoldOut)
        {
            await _publisher.PublishAsync(CommerceWebhookEvents.StockSoldOut, ToData(context), cancellationToken);
        }
        else if (context.RanLow)
        {
            await _publisher.PublishAsync(CommerceWebhookEvents.StockLow, ToData(context), cancellationToken);
        }
    }

    private static object ToData(StockLevelChangedContext context)
        => new { stock = StockItemApiMapper.ToApi(context.Item), previousAvailable = context.PreviousAvailable };
}
