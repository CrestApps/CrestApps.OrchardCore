namespace CrestApps.OrchardCore.WorkQueues;

/// <summary>
/// The state of an item on a leased work queue.
/// </summary>
/// <remarks>
/// The values match the status enums of the records that kept their own copy of this state before the queue existed
/// (the Contact Center outbox and the provider webhook inbox), so those records map onto it by value.
/// </remarks>
public enum LeasedWorkItemStatus
{
    /// <summary>
    /// Waiting for its next attempt.
    /// </summary>
    Pending,

    /// <summary>
    /// Owned by one worker until its lease expires.
    /// </summary>
    Claimed,

    /// <summary>
    /// Done. Queues that keep completed items (for example as idempotency tombstones) leave them in this state.
    /// </summary>
    Completed,

    /// <summary>
    /// Given up on, after its attempts ran out or because it can never succeed.
    /// </summary>
    DeadLettered,
}
