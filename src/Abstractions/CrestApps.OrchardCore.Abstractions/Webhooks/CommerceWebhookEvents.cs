namespace CrestApps.OrchardCore.Commerce.Webhooks;

/// <summary>
/// The names of the events the commerce modules publish through the Webhooks building block
/// (<c>IWebhookPublisher</c>, declared with <c>AddWebhookEvent</c>).
/// </summary>
public static class CommerceWebhookEvents
{
    public const string OrderCreated = "order.created";

    public const string OrderPaid = "order.paid";

    public const string OrderCanceled = "order.canceled";

    public const string OrderRefunded = "order.refunded";

    public const string ShipmentShipped = "shipment.shipped";

    public const string ShipmentDelivered = "shipment.delivered";

    public const string CustomerCreated = "customer.created";

    public const string StockLow = "stock.low";

    public const string StockSoldOut = "stock.sold-out";

    public const string StockChanged = "stock.changed";
}
