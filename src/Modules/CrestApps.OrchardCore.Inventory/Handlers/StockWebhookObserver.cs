using CrestApps.OrchardCore.Commerce.Webhooks;
using CrestApps.OrchardCore.Inventory.Api;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Inventory.Handlers;

/// <summary>
/// Tells webhooks of every change to what's available, once the work that changed it is saved, so another system (a
/// marketplace listing, a warehouse) keeps the same count. Several changes to one item in one unit of work are one
/// event, from the count before the first to the count after the last.
/// </summary>
internal sealed class StockWebhookObserver : IStockLevelObserver
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

            ShellScope.AddDeferredTask(async scope =>
            {
                var publisher = scope.ServiceProvider.GetRequiredService<ICommerceWebhookPublisher>();
                var items = scope.ServiceProvider.GetRequiredService<IStockItemStore>();

                foreach (var (stockItemId, before) in previous)
                {
                    if (await items.FindByIdAsync(stockItemId) is { Archived: false } changed && changed.Available != before)
                    {
                        await publisher.PublishAsync(CommerceWebhookEvents.StockChanged, new { stock = InventoryAdminApiEndpoints.ToApi(changed), previousAvailable = before });
                    }
                }
            });
        }

        _previous.TryAdd(item.ItemId, previousAvailable);
    }
}
