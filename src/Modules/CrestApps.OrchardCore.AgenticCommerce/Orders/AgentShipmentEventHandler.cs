using CrestApps.OrchardCore.Shipping.Services;

namespace CrestApps.OrchardCore.AgenticCommerce.Orders;

/// <summary>
/// Tells agents' platforms when the orders their agents placed ship and arrive.
/// </summary>
public sealed class AgentShipmentEventHandler : ShipmentEventHandlerBase
{
    private readonly AgentOrderUpdates _updates;

    public AgentShipmentEventHandler(AgentOrderUpdates updates)
    {
        _updates = updates;
    }

    public override Task ShippedAsync(ShipmentContext context)
        => _updates.PublishAsync(context.Order, created: false);

    public override Task DeliveredAsync(ShipmentContext context)
        => _updates.PublishAsync(context.Order, created: false);

    public override Task ReturnedAsync(ShipmentContext context)
        => _updates.PublishAsync(context.Order, created: false);
}
