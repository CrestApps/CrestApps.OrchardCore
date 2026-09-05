namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies the result of accepting a provider webhook delivery into the durable inbox.
/// </summary>
public enum ProviderWebhookInboxAcceptanceStatus
{
    /// <summary>
    /// A new durable inbox message was committed.
    /// </summary>
    Accepted,

    /// <summary>
    /// The delivery was already committed and remains idempotently accepted.
    /// </summary>
    Duplicate,

    /// <summary>
    /// The delivery could not acquire its distributed acceptance lock.
    /// </summary>
    Busy,
}
