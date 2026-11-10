using CrestApps.OrchardCore.Webhooks.Models;

namespace CrestApps.OrchardCore.Webhooks;

/// <summary>
/// Sends a webhook to one receiver that isn't a subscription, such as an AI agent platform that asked for a protocol's
/// order updates. The delivery is kept, retried with the same backoff as subscriptions and logged, and a named
/// <see cref="IWebhookDeliveryFormatter"/> adds the headers and signature the receiver's protocol asks for.
/// </summary>
public interface IWebhookOutbox
{
    /// <summary>
    /// Queues a delivery. It is sent after the work that raised it is saved, and retried until the receiver takes it.
    /// </summary>
    /// <param name="delivery">What to send, and where.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task EnqueueAsync(WebhookDirectDelivery delivery, CancellationToken cancellationToken = default);
}
