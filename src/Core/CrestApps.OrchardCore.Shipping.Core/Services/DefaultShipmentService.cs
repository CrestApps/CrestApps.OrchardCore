using CrestApps.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Shipping.Models;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Shipping.Services;

public sealed class DefaultShipmentService : IShipmentService
{
    private const int MaxLength = 255;

    private readonly IShipmentStore _shipments;
    private readonly IOrderStore _orders;
    private readonly ISiteService _siteService;
    private readonly IEnumerable<IShipmentEventHandler> _handlers;
    private readonly IClock _clock;

    public DefaultShipmentService(
        IShipmentStore shipments,
        IOrderStore orders,
        ISiteService siteService,
        IEnumerable<IShipmentEventHandler> handlers,
        IClock clock)
    {
        _shipments = shipments;
        _orders = orders;
        _siteService = siteService;
        _handlers = handlers;
        _clock = clock;
    }

    public async Task<IReadOnlyDictionary<string, int>> GetRemainingAsync(Order order, CancellationToken cancellationToken = default)
        => GetRemaining(order, await _shipments.ListByOrderAsync(order.ItemId, cancellationToken));

    public async Task<ShipmentResult> CreateAsync(Order order, IEnumerable<ShipmentLineRequest> lines, ShipmentDetails details, bool ship, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (order.Status != OrderStatus.Paid)
        {
            return ShipmentResult.Fail(ShipmentError.OrderNotPaid);
        }

        var existing = await _shipments.ListByOrderAsync(order.ItemId, cancellationToken);
        var remaining = GetRemaining(order, existing);
        var requested = (lines ?? [])
            .Where(line => line is { Quantity: > 0 } && !string.IsNullOrEmpty(line.LineId))
            .GroupBy(line => line.LineId, StringComparer.Ordinal)
            .Select(group => (LineId: group.Key, Quantity: group.Sum(line => line.Quantity)))
            .ToArray();

        if (requested.Length == 0)
        {
            return ShipmentResult.Fail(ShipmentError.NothingToShip);
        }

        if (requested.Any(line => line.Quantity > remaining.GetValueOrDefault(line.LineId)))
        {
            return ShipmentResult.Fail(ShipmentError.TooMany);
        }

        var now = _clock.UtcNow;
        var shipment = new Shipment
        {
            ItemId = UniqueId.GenerateId(),
            OrderId = order.ItemId,
            OrderNumber = order.OrderNumber,
            Number = existing.Count == 0 ? 1 : existing.Max(entry => entry.Number) + 1,
            Lines = [.. requested.Select(line => ToShipmentLine(order, line.LineId, line.Quantity))],
            Status = ShipmentStatus.Pending,
            CreatedUtc = now,
            ModifiedUtc = now,
        };

        await ApplyAsync(shipment, details);
        await _shipments.CreateAsync(shipment, cancellationToken);

        var context = new ShipmentContext(order, shipment);

        foreach (var handler in _handlers)
        {
            await handler.CreatedAsync(context);
        }

        if (ship)
        {
            return await ShipAsync(order, [.. existing, shipment], shipment, details: null, cancellationToken);
        }

        await UpdateFulfillmentAsync(order, [.. existing, shipment], cancellationToken);

        return ShipmentResult.Success(shipment);
    }

    public async Task<ShipmentResult> ShipAsync(Shipment shipment, ShipmentDetails details, CancellationToken cancellationToken = default)
    {
        var (order, all) = await LoadAsync(shipment, cancellationToken);

        return order is null ? ShipmentResult.Fail(ShipmentError.InvalidStatus) : await ShipAsync(order, all, shipment, details, cancellationToken);
    }

    public Task<ShipmentResult> DeliverAsync(Shipment shipment, CancellationToken cancellationToken = default)
        => ChangeAsync(shipment, ShipmentStatus.Shipped, ShipmentStatus.Delivered, (entry, now) => entry.DeliveredUtc = now, (handler, context) => handler.DeliveredAsync(context), cancellationToken);

    public Task<ShipmentResult> ReturnAsync(Shipment shipment, CancellationToken cancellationToken = default)
        => ChangeAsync(shipment, null, ShipmentStatus.Returned, (entry, now) => entry.ReturnedUtc = now, (handler, context) => handler.ReturnedAsync(context), cancellationToken);

    public async Task<ShipmentResult> DeleteAsync(Shipment shipment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shipment);

        if (shipment.Status != ShipmentStatus.Pending)
        {
            return ShipmentResult.Fail(ShipmentError.InvalidStatus);
        }

        var (order, all) = await LoadAsync(shipment, cancellationToken);

        await _shipments.DeleteAsync(shipment, cancellationToken);

        if (order is not null)
        {
            await UpdateFulfillmentAsync(order, [.. all.Where(entry => entry.ItemId != shipment.ItemId)], cancellationToken);
        }

        return ShipmentResult.Success(shipment);
    }

    /// <summary>
    /// Gets how many units of each of the order's goods aren't in a shipment yet, ignoring returned shipments.
    /// </summary>
    public static IReadOnlyDictionary<string, int> GetRemaining(Order order, IEnumerable<Shipment> shipments)
    {
        var allocated = Allocated(shipments, status => status != ShipmentStatus.Returned);

        return (order?.Lines ?? [])
            .Where(ShippingOrderRateService.IsPhysical)
            .GroupBy(line => line.LineId, StringComparer.Ordinal)
            .Select(group => (LineId: group.Key, Left: group.Sum(line => line.Quantity) - allocated.GetValueOrDefault(group.Key)))
            .Where(line => line.Left > 0)
            .ToDictionary(line => line.LineId, line => line.Left, StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets how far the order is shipped: nothing, some, or all of its goods in shipments that left. An order with no
    /// goods has no fulfillment status.
    /// </summary>
    public static OrderFulfillmentStatus? GetFulfillmentStatus(Order order, IEnumerable<Shipment> shipments)
    {
        var goods = (order?.Lines ?? []).Where(ShippingOrderRateService.IsPhysical).ToArray();

        if (goods.Length == 0)
        {
            return null;
        }

        var shipped = Allocated(shipments, status => status is ShipmentStatus.Shipped or ShipmentStatus.Delivered);
        var total = goods.Sum(line => line.Quantity);
        var sent = goods
            .GroupBy(line => line.LineId, StringComparer.Ordinal)
            .Sum(group => Math.Min(group.Sum(line => line.Quantity), shipped.GetValueOrDefault(group.Key)));

        return sent <= 0
            ? OrderFulfillmentStatus.Unfulfilled
            : sent >= total ? OrderFulfillmentStatus.Fulfilled : OrderFulfillmentStatus.PartiallyFulfilled;
    }

    private async Task<ShipmentResult> ShipAsync(Order order, IReadOnlyList<Shipment> all, Shipment shipment, ShipmentDetails details, CancellationToken cancellationToken)
    {
        if (shipment.Status != ShipmentStatus.Pending)
        {
            return ShipmentResult.Fail(ShipmentError.InvalidStatus);
        }

        if (details is not null)
        {
            await ApplyAsync(shipment, details);
        }

        var now = _clock.UtcNow;

        shipment.Status = ShipmentStatus.Shipped;
        shipment.ShippedUtc = now;
        shipment.ModifiedUtc = now;

        var context = new ShipmentContext(order, shipment);

        foreach (var handler in _handlers)
        {
            await handler.ShippedAsync(context);
        }

        await _shipments.UpdateAsync(shipment, cancellationToken);
        await UpdateFulfillmentAsync(order, all, cancellationToken);

        return ShipmentResult.Success(shipment);
    }

    private async Task<ShipmentResult> ChangeAsync(
        Shipment shipment,
        ShipmentStatus? from,
        ShipmentStatus to,
        Action<Shipment, DateTime> stamp,
        Func<IShipmentEventHandler, ShipmentContext, Task> raise,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shipment);

        // Delivered follows shipped; a shipment comes back only once it has left.
        var allowed = from is { } required
            ? shipment.Status == required
            : shipment.Status is ShipmentStatus.Shipped or ShipmentStatus.Delivered;

        if (!allowed)
        {
            return ShipmentResult.Fail(ShipmentError.InvalidStatus);
        }

        var (order, all) = await LoadAsync(shipment, cancellationToken);

        if (order is null)
        {
            return ShipmentResult.Fail(ShipmentError.InvalidStatus);
        }

        var now = _clock.UtcNow;

        shipment.Status = to;
        shipment.ModifiedUtc = now;
        stamp(shipment, now);

        var context = new ShipmentContext(order, shipment);

        foreach (var handler in _handlers)
        {
            await raise(handler, context);
        }

        await _shipments.UpdateAsync(shipment, cancellationToken);
        await UpdateFulfillmentAsync(order, all, cancellationToken);

        return ShipmentResult.Success(shipment);
    }

    private async Task<(Order Order, IReadOnlyList<Shipment> All)> LoadAsync(Shipment shipment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shipment);

        var order = await _orders.FindByIdAsync(shipment.OrderId, cancellationToken);

        if (order is null)
        {
            return (null, []);
        }

        // The shipment being changed, as changed, in place of its stored copy.
        var all = (await _shipments.ListByOrderAsync(order.ItemId, cancellationToken))
            .Select(entry => entry.ItemId == shipment.ItemId ? shipment : entry)
            .ToList();

        if (!all.Contains(shipment))
        {
            all.Add(shipment);
        }

        return (order, all);
    }

    private async Task UpdateFulfillmentAsync(Order order, IReadOnlyList<Shipment> all, CancellationToken cancellationToken)
    {
        var status = GetFulfillmentStatus(order, all);

        if (order.FulfillmentStatus == status)
        {
            return;
        }

        order.FulfillmentStatus = status;
        order.ModifiedUtc = _clock.UtcNow;

        await _orders.UpdateAsync(order, cancellationToken);
    }

    private async Task ApplyAsync(Shipment shipment, ShipmentDetails details)
    {
        if (details is null)
        {
            return;
        }

        shipment.Carrier = Clean(details.Carrier);
        shipment.TrackingNumber = Clean(details.TrackingNumber);
        shipment.Note = Clean(details.Note);
        shipment.TrackingUrl = Clean(details.TrackingUrl);

        if (shipment.TrackingUrl is not null && (!Uri.TryCreate(shipment.TrackingUrl, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https")))
        {
            // Only a web link is shown to the buyer.
            shipment.TrackingUrl = null;
        }

        if (shipment.TrackingUrl is null && shipment.Carrier is not null && shipment.TrackingNumber is not null)
        {
            var carrier = (await _siteService.GetSettingsAsync<ShippingSettings>()).Carriers?
                .FirstOrDefault(entry => string.Equals(entry.Name, shipment.Carrier, StringComparison.OrdinalIgnoreCase));

            shipment.TrackingUrl = carrier?.GetTrackingUrl(shipment.TrackingNumber);
        }
    }

    private static ShipmentLine ToShipmentLine(Order order, string lineId, int quantity)
    {
        var line = order.Lines.First(entry => entry.LineId == lineId);

        return new ShipmentLine
        {
            LineId = lineId,
            // The add-ons ride along, so whoever packs it sees the engraving to do.
            Title = OrderLineText.DescribeForPacking(line),
            Sku = line.Sku,
            Quantity = quantity,
        };
    }

    private static Dictionary<string, int> Allocated(IEnumerable<Shipment> shipments, Func<ShipmentStatus, bool> counts)
    {
        var allocated = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var shipment in (shipments ?? []).Where(entry => counts(entry.Status)))
        {
            foreach (var line in shipment.Lines ?? [])
            {
                allocated[line.LineId] = allocated.GetValueOrDefault(line.LineId) + line.Quantity;
            }
        }

        return allocated;
    }

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, MaxLength)];
}
