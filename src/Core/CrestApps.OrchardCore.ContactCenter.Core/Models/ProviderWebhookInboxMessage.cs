using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents an authenticated provider webhook delivery retained until processing succeeds or is dead-lettered.
/// </summary>
public sealed class ProviderWebhookInboxMessage : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the canonical provider technical name.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider-scoped idempotency key.
    /// </summary>
    public string DeliveryId { get; set; }

    /// <summary>
    /// Gets or sets the stable technical name of the payload handler.
    /// </summary>
    public string HandlerName { get; set; }

    /// <summary>
    /// Gets or sets the normalized serialized payload.
    /// </summary>
    public string Payload { get; set; }

    /// <summary>
    /// Gets or sets the durable processing status.
    /// </summary>
    public ProviderWebhookInboxStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the opaque token identifying the worker that owns the current claim.
    /// </summary>
    public string OwnerToken { get; set; }

    /// <summary>
    /// Gets or sets the monotonically increasing fence token assigned to the current claim.
    /// </summary>
    public long FenceToken { get; set; }

    /// <summary>
    /// Gets or sets the number of failed processing attempts.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the next processing attempt is due.
    /// </summary>
    public DateTime NextAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets the exception type from the last failed processing attempt.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the message was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the message was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
