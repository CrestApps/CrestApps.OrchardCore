using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Manages the lifecycle of queue items as activities enter and leave Contact Center queues.
/// </summary>
public interface IActivityQueueService
{
    /// <summary>
    /// Adds a CRM activity to a queue so it can be routed to an agent.
    /// </summary>
    /// <param name="activityItemId">The CRM activity identifier.</param>
    /// <param name="queueId">The queue identifier.</param>
    /// <param name="priority">The optional priority override; the queue default is used when null.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The created queue item.</returns>
    Task<QueueItem> EnqueueAsync(string activityItemId, string queueId, InteractionPriority? priority, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a queue item from its queue with the supplied final status.
    /// </summary>
    /// <param name="queueItem">The queue item to dequeue.</param>
    /// <param name="status">The final status to record.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task DequeueAsync(QueueItem queueItem, QueueItemStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves waiting items from the queue to its configured overflow queue when they have waited past the
    /// overflow threshold, or when the queue is closed and configured to overflow after hours.
    /// </summary>
    /// <param name="queue">The queue whose waiting items are evaluated for overflow.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of items moved to the overflow queue.</returns>
    Task<int> OverflowDueAsync(ActivityQueue queue, CancellationToken cancellationToken = default);
}
