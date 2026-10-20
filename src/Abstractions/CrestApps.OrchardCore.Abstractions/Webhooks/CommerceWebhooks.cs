using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Commerce.Webhooks;

/// <summary>
/// Sends an event to every webhook subscribed to it. A module publishes its events through here; with the Commerce
/// Webhooks feature off there is no publisher, so a module resolves it with <c>GetService</c> and skips when it's null.
/// </summary>
public interface ICommerceWebhookPublisher
{
    /// <summary>
    /// Queues the event for every enabled webhook subscribed to it. It's sent once the request's work is saved, and
    /// again with backoff until the receiver accepts it.
    /// </summary>
    /// <param name="eventName">The event's name, such as <c>order.paid</c>.</param>
    /// <param name="data">What the event is about, serialized as JSON in the payload's <c>data</c>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task PublishAsync(string eventName, object data, CancellationToken cancellationToken = default);
}

/// <summary>
/// An event webhooks can subscribe to.
/// </summary>
/// <param name="Name">The event's name, such as <c>order.paid</c>.</param>
/// <param name="DisplayName">What the merchant sees when choosing events.</param>
/// <param name="Group">What it's about, to group events by: Orders, Customers, Stock.</param>
public sealed record CommerceWebhookEvent(string Name, string DisplayName, string Group);

/// <summary>
/// The events webhooks can subscribe to. Each module adds its own with
/// <see cref="CommerceWebhookServiceCollectionExtensions.AddCommerceWebhookEvent"/>.
/// </summary>
public sealed class CommerceWebhookOptions
{
    public IList<CommerceWebhookEvent> Events { get; } = [];
}

public static class CommerceWebhookServiceCollectionExtensions
{
    /// <summary>
    /// Offers an event to webhooks.
    /// </summary>
    public static IServiceCollection AddCommerceWebhookEvent(this IServiceCollection services, string name, string displayName, string group)
        => services.Configure<CommerceWebhookOptions>(options =>
        {
            if (!options.Events.Any(entry => string.Equals(entry.Name, name, StringComparison.Ordinal)))
            {
                options.Events.Add(new CommerceWebhookEvent(name, displayName, group));
            }
        });
}

/// <summary>
/// The names of the events the commerce modules publish.
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

    /// <summary>
    /// The event a merchant sends from a webhook's page, to try the receiver.
    /// </summary>
    public const string Ping = "webhook.ping";
}
