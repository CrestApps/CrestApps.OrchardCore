namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Carries the queue item and agent candidates through routing strategies.
/// </summary>
public sealed class ActivityRoutingContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityRoutingContext"/> class.
    /// </summary>
    /// <param name="queue">The queue being routed.</param>
    /// <param name="queueItem">The queue item being assigned.</param>
    /// <param name="candidates">The candidate agents.</param>
    public ActivityRoutingContext(
        ActivityQueue queue,
        QueueItem queueItem,
        IEnumerable<ActivityRoutingCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(queueItem);
        ArgumentNullException.ThrowIfNull(candidates);

        Queue = queue;
        QueueItem = queueItem;
        Candidates = candidates.ToList();
    }

    /// <summary>
    /// Gets the queue being routed.
    /// </summary>
    public ActivityQueue Queue { get; }

    /// <summary>
    /// Gets the queue item being assigned.
    /// </summary>
    public QueueItem QueueItem { get; }

    /// <summary>
    /// Gets the candidate agents that routing strategies can score or reject.
    /// </summary>
    public IList<ActivityRoutingCandidate> Candidates { get; }
}
