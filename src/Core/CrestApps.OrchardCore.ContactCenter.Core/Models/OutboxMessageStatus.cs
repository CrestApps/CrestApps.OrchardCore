namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Identifies the dispatch state of a Contact Center outbox message.
/// </summary>
public enum OutboxMessageStatus
{
    /// <summary>
    /// The message is waiting to be retried after a failed handler dispatch.
    /// </summary>
    Pending,

    /// <summary>
    /// The message is owned by a worker under a durable, expiring claim.
    /// </summary>
    Claimed,

    /// <summary>
    /// Every registered handler completed and the message is safe to remove.
    /// </summary>
    Completed,

    /// <summary>
    /// The message exhausted its retry budget and was set aside for manual inspection.
    /// </summary>
    DeadLettered,
}
