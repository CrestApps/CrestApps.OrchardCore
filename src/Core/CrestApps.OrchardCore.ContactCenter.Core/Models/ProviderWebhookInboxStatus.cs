namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Identifies the durable processing state of a provider webhook inbox message.
/// </summary>
public enum ProviderWebhookInboxStatus
{
    /// <summary>
    /// The message is pending processing or retry.
    /// </summary>
    Pending,

    /// <summary>
    /// The message is owned by a worker under a durable, expiring claim.
    /// </summary>
    Claimed,

    /// <summary>
    /// The normalized payload completed and the message is retained as an idempotency tombstone.
    /// </summary>
    Completed,

    /// <summary>
    /// The message exhausted its retry budget and requires operator intervention.
    /// </summary>
    DeadLettered,
}
