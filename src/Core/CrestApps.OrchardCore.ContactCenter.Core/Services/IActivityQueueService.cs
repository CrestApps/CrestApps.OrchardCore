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
}
