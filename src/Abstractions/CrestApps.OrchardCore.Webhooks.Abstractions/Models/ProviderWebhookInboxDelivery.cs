namespace CrestApps.OrchardCore.Webhooks.Models;

/// <summary>
/// Represents an authenticated, normalized provider webhook delivery ready for durable acceptance.
/// </summary>
public sealed class ProviderWebhookInboxDelivery
{
    /// <summary>
    /// Gets or sets the canonical provider technical name.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider-scoped idempotency key for the delivery.
    /// </summary>
    public string DeliveryId { get; set; }

    /// <summary>
    /// Gets or sets the stable technical name of the inbox handler that owns the payload.
    /// </summary>
    public string HandlerName { get; set; }

    /// <summary>
    /// Gets or sets the normalized serialized payload.
    /// </summary>
    public string Payload { get; set; }

    /// <summary>
    /// Gets or sets the W3C <c>traceparent</c> of the request that delivered the payload, captured with
    /// <c>TraceContextPropagation.Capture()</c> when the payload was received, or <see langword="null"/> to have the
    /// inbox capture the trace current when the delivery is accepted. The dispatch that handles the delivery, inline or
    /// on the retry pass, links back to it.
    /// </summary>
    public string TraceParent { get; set; }
}
