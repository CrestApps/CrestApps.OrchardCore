using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Inventory.Services;

/// <summary>
/// Answers from the stock items: what is available at the store's location. For a cart, the units its own unpaid
/// orders hold count as available to it.
/// </summary>
public sealed class InventoryAvailabilityProvider : IProductAvailabilityProvider
{
    private readonly ISession _session;
    private readonly IServiceProvider _serviceProvider;
    private readonly global::OrchardCore.Modules.IClock _clock;

    public InventoryAvailabilityProvider(ISession session, IServiceProvider serviceProvider, global::OrchardCore.Modules.IClock clock)
    {
        _session = session;
        _serviceProvider = serviceProvider;
        _clock = clock;
    }

    public async Task<IReadOnlyDictionary<ProductStockKey, ProductAvailability>> GetAvailabilityAsync(ProductAvailabilityRequest request, CancellationToken cancellationToken = default)
    {
        var answer = new Dictionary<ProductStockKey, ProductAvailability>();
        var contentItemIds = request?.Products?.Select(key => key.ContentItemId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray() ?? [];

        if (contentItemIds.Length == 0)
        {
            return answer;
        }

        var items = (await _session.Query<StockItem, StockItemIndex>(index =>
                index.ContentItemId.IsIn(contentItemIds) &&
                !index.Archived &&
                index.LocationId == InventoryConstants.DefaultLocationId,
                collection: InventoryConstants.CollectionName)
            .ListAsync(cancellationToken)).ToArray();

        if (items.Length == 0)
        {
            return answer;
        }

        var ownHolds = await GetCartHoldsAsync(request.CartId, items, cancellationToken);
        var wanted = request.Products.ToHashSet();

        foreach (var item in items)
        {
            var key = new ProductStockKey(item.ContentItemId, item.VariantId);

            // A product asked about as a whole gets each of its variants, so a listing can combine them.
            if (!wanted.Contains(key) && !wanted.Contains(new ProductStockKey(item.ContentItemId, null)))
            {
                continue;
            }

            // A release date to come makes a backorder a pre-order; once it's passed, a backorder is a backorder.
            answer[key] = new ProductAvailability(item.Available + ownHolds.GetValueOrDefault(item.ItemId), item.AllowBackorder, item.LowStockThreshold)
            {
                ReleaseDateUtc = item.ReleaseDateUtc > _clock.UtcNow ? item.ReleaseDateUtc : null,
            };
        }

        return answer;
    }

    private async Task<Dictionary<string, int>> GetCartHoldsAsync(string cartId, StockItem[] items, CancellationToken cancellationToken)
    {
        var holds = new Dictionary<string, int>(StringComparer.Ordinal);

        if (string.IsNullOrEmpty(cartId) || _serviceProvider.GetService<IOrderStore>() is not { } orders)
        {
            return holds;
        }

        var orderIds = (await orders.ListUnpaidByCartAsync(cartId, cancellationToken)).Select(order => order.ItemId).ToArray();

        if (orderIds.Length == 0)
        {
            return holds;
        }

        var itemIds = items.Select(item => item.ItemId).ToArray();
        var reservations = await _session.Query<StockReservation, StockReservationIndex>(index =>
                index.OrderId.IsIn(orderIds) &&
                index.StockItemId.IsIn(itemIds) &&
                index.Status == StockReservationStatus.Held,
                collection: InventoryConstants.CollectionName)
            .ListAsync(cancellationToken);

        foreach (var reservation in reservations)
        {
            holds[reservation.StockItemId] = holds.GetValueOrDefault(reservation.StockItemId) + reservation.Quantity;
        }

        return holds;
    }
}
