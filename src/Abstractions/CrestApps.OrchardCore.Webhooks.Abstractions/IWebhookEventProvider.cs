using CrestApps.OrchardCore.Webhooks.Models;

namespace CrestApps.OrchardCore.Webhooks;

/// <summary>
/// Declares the events a module publishes to webhooks. Register one in a startup gated on the feature that publishes
/// the events; the subscription editor lists every declared event, and the publisher only sends declared ones.
/// <see cref="WebhookEventServiceCollectionExtensions.AddWebhookEvent"/> declares a single event without a class.
/// </summary>
public interface IWebhookEventProvider
{
    /// <summary>
    /// Returns the events the module publishes.
    /// </summary>
    /// <returns>The events.</returns>
    IEnumerable<WebhookEventDescriptor> GetEvents();
}
