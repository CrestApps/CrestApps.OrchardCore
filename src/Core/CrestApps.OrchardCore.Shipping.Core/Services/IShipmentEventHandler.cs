using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Shipping.Models;

namespace CrestApps.OrchardCore.Shipping.Services;

/// <summary>
/// Hears about shipments as they change, in the same unit of work: to email the buyer, start a workflow, or tell a
/// carrier.
/// </summary>
public interface IShipmentEventHandler
{
    Task CreatedAsync(ShipmentContext context);

    Task ShippedAsync(ShipmentContext context);

    Task DeliveredAsync(ShipmentContext context);

    Task ReturnedAsync(ShipmentContext context);
}

public abstract class ShipmentEventHandlerBase : IShipmentEventHandler
{
    public virtual Task CreatedAsync(ShipmentContext context)
        => Task.CompletedTask;

    public virtual Task ShippedAsync(ShipmentContext context)
        => Task.CompletedTask;

    public virtual Task DeliveredAsync(ShipmentContext context)
        => Task.CompletedTask;

    public virtual Task ReturnedAsync(ShipmentContext context)
        => Task.CompletedTask;
}

public sealed class ShipmentContext
{
    public ShipmentContext(Order order, Shipment shipment)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(shipment);

        Order = order;
        Shipment = shipment;
    }

    public Order Order { get; }

    public Shipment Shipment { get; }
}
