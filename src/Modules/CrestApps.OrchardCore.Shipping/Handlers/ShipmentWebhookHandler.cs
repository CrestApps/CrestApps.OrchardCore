using CrestApps.OrchardCore.Commerce.Webhooks;
using CrestApps.OrchardCore.Shipping.Api;
using CrestApps.OrchardCore.Shipping.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Shipping.Handlers;

/// <summary>
/// Tells webhooks when a shipment leaves and when it arrives, with its tracking, in the commerce API's shape.
/// </summary>
internal sealed class ShipmentWebhookHandler : ShipmentEventHandlerBase
{
    private readonly IServiceProvider _serviceProvider;

    public ShipmentWebhookHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override Task ShippedAsync(ShipmentContext context)
        => PublishAsync(CommerceWebhookEvents.ShipmentShipped, context);

    public override Task DeliveredAsync(ShipmentContext context)
        => PublishAsync(CommerceWebhookEvents.ShipmentDelivered, context);

    private Task PublishAsync(string eventName, ShipmentContext context)
        => _serviceProvider.GetService<ICommerceWebhookPublisher>() is { } publisher
            ? publisher.PublishAsync(eventName, new
            {
                shipment = ShipmentApiEndpoints.ToApi(context.Shipment),
                orderNumber = context.Order.OrderNumber,
                customerEmail = context.Order.CustomerEmail,
            })
            : Task.CompletedTask;
}
