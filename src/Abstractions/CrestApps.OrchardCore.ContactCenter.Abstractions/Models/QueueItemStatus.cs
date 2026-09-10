namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies the lifecycle state of a queued activity waiting for an agent.
/// </summary>
public enum QueueItemStatus
{
    /// <summary>
    /// The item is waiting in the queue for routing.
    /// </summary>
    Waiting,

    /// <summary>
    /// The item is reserved for an agent and awaiting acceptance.
    /// </summary>
    Reserved,

    /// <summary>
    /// The item was assigned to an agent.
    /// </summary>
    Assigned,

    /// <summary>
    /// The item left the queue because work completed.
    /// </summary>
    Completed,

    /// <summary>
    /// The item left the queue without being completed (overflow, abandon, cancel).
    /// </summary>
    Removed,
}
