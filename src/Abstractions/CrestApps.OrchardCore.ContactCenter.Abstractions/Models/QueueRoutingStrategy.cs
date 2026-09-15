namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies the strategy a queue uses to pick which available agent receives the next queued activity.
/// </summary>
public enum QueueRoutingStrategy
{
    /// <summary>
    /// Offers work to the agent who has been available the longest.
    /// </summary>
    LongestIdle,

    /// <summary>
    /// Distributes work fairly by offering to the agent who least recently received an assignment.
    /// </summary>
    RoundRobin,

    /// <summary>
    /// Offers work to the agent currently handling the fewest active interactions.
    /// </summary>
    LeastBusy,
}
