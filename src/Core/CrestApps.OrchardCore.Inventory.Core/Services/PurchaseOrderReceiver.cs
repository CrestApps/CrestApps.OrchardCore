using CrestApps.OrchardCore.Inventory.Core.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Inventory.Core.Services;

/// <summary>
/// Receives a purchase order's stock: each line's units arriving are added to stock at the order's location, never
/// more than are still to come, and the order is received once nothing is.
/// </summary>
public sealed class PurchaseOrderReceiver
{
    private readonly IInventoryService _inventory;
    private readonly IPurchaseOrderStore _orders;
    private readonly IClock _clock;

    public PurchaseOrderReceiver(IInventoryService inventory, IPurchaseOrderStore orders, IClock clock)
    {
        _inventory = inventory;
        _orders = orders;
        _clock = clock;
    }

    /// <summary>
    /// Receives units of the order's lines, by line id, and returns how many were received.
    /// </summary>
    public async Task<int> ReceiveAsync(PurchaseOrder order, IReadOnlyDictionary<string, int> quantities, StockActor actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (!order.CanReceive || quantities is null)
        {
            return 0;
        }

        var received = 0;

        foreach (var line in order.Lines)
        {
            if (!quantities.TryGetValue(line.LineId, out var quantity))
            {
                continue;
            }

            quantity = Math.Min(quantity, line.Outstanding);

            if (quantity <= 0)
            {
                continue;
            }

            var result = await _inventory.ReceiveAsync(line.StockItemId, order.LocationId, quantity, order.ItemId, order.Number, actor, cancellationToken);

            // A line whose stock item is gone can't be received; the rest still are.
            if (!result.Succeeded)
            {
                continue;
            }

            line.Received += quantity;
            received += quantity;
        }

        if (received > 0)
        {
            var now = _clock.UtcNow;

            order.Status = order.Lines.All(line => line.Outstanding == 0) ? PurchaseOrderStatus.Received : PurchaseOrderStatus.PartiallyReceived;
            order.ReceivedUtc = order.Status == PurchaseOrderStatus.Received ? now : null;
            order.ModifiedUtc = now;

            await _orders.UpdateAsync(order, cancellationToken);
        }

        return received;
    }
}
