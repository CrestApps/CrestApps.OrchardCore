namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

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
}
