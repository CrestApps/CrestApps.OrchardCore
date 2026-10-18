using CrestApps.OrchardCore.Shipping.Services;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Shipping.Workflows;

/// <summary>
/// Starts the shipment workflow events as shipments change.
/// </summary>
internal sealed class ShipmentWorkflowHandler : IShipmentEventHandler
{
    private readonly IWorkflowManager _workflowManager;

    public ShipmentWorkflowHandler(IWorkflowManager workflowManager)
    {
        _workflowManager = workflowManager;
    }

    public Task CreatedAsync(ShipmentContext context)
        => TriggerAsync(nameof(ShipmentCreatedEvent), context);

    public Task ShippedAsync(ShipmentContext context)
        => TriggerAsync(nameof(ShipmentShippedEvent), context);

    public Task DeliveredAsync(ShipmentContext context)
        => TriggerAsync(nameof(ShipmentDeliveredEvent), context);

    public Task ReturnedAsync(ShipmentContext context)
        => TriggerAsync(nameof(ShipmentReturnedEvent), context);

    private async Task TriggerAsync(string eventName, ShipmentContext context)
    {
        var shipment = context.Shipment;
        var input = new Dictionary<string, object>
        {
            ["Order"] = context.Order,
            ["Shipment"] = shipment,
            ["OrderId"] = context.Order.ItemId,
            ["OrderNumber"] = context.Order.OrderNumber,
            ["ShipmentId"] = shipment.ItemId,
            ["ShipmentNumber"] = shipment.Number,
            ["Carrier"] = shipment.Carrier,
            ["TrackingNumber"] = shipment.TrackingNumber,
            ["TrackingUrl"] = shipment.TrackingUrl,
            ["CustomerEmail"] = context.Order.CustomerEmail,
        };

        await _workflowManager.TriggerEventAsync(eventName, input, correlationId: shipment.ItemId);
    }
}
