using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the persistence contract for queue items.
/// </summary>
public interface IQueueItemStore : ICatalog<QueueItem>
{
    /// <summary>
    /// Lists the items waiting in the specified queue, highest priority and oldest first.
    /// </summary>
    /// <param name="queueId">The queue identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The waiting items ordered for routing.</returns>
    Task<IReadOnlyCollection<QueueItem>> GetWaitingAsync(string queueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the active queue item for the specified activity.
    /// </summary>
    /// <param name="activityItemId">The CRM activity identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching queue item, or <see langword="null"/> when none exists.</returns>
    Task<QueueItem> FindByActivityIdAsync(string activityItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the items currently waiting in the specified queue using an aggregate query, without
    /// materializing the waiting rows.
    /// </summary>
    /// <param name="queueId">The queue identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of waiting items in the queue.</returns>
    Task<int> CountWaitingAsync(string queueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the items currently waiting across every queue using an aggregate query, without materializing the
    /// waiting rows. This is the tenant-wide queued-interaction backlog an operator weighs before draining a node.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The total number of waiting items across all queues.</returns>
    Task<int> CountAllWaitingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the items currently waiting in each of the specified queues using a single aggregate query.
    /// Queues with no waiting items are absent from the result rather than present with a zero.
    /// </summary>
    /// <param name="queueIds">The queue identifiers to count.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of waiting items keyed by queue identifier.</returns>
    Task<IReadOnlyDictionary<string, int>> CountWaitingByQueueIdsAsync(
        IReadOnlyCollection<string> queueIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the items waiting in the specified queue that were enqueued before the threshold, using an
    /// aggregate query. This supports SLA-breach counting without loading the waiting rows.
    /// </summary>
    /// <param name="queueId">The queue identifier.</param>
    /// <param name="thresholdUtc">The UTC instant before which a waiting item is counted.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of waiting items enqueued before <paramref name="thresholdUtc"/>.</returns>
    Task<int> CountWaitingOlderThanAsync(
        string queueId,
        DateTime thresholdUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the single next waiting item to route in the specified queue using a bounded top-one query,
    /// ordered highest priority then oldest first, without loading the whole waiting backlog. This is the
    /// routing winner whenever the queue does not apply SLA aging, so callers can avoid materializing every
    /// waiting row on the assignment hot path.
    /// </summary>
    /// <param name="queueId">The queue identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The next waiting item to route, or <see langword="null"/> when the queue has none waiting.</returns>
    Task<QueueItem> FindNextWaitingAsync(string queueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the single oldest waiting item in the specified queue using a bounded top-one query, without
    /// loading the whole waiting backlog. This supports longest-wait measurement.
    /// </summary>
    /// <param name="queueId">The queue identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The oldest waiting item, or <see langword="null"/> when the queue has none waiting.</returns>
    Task<QueueItem> FindLongestWaitingAsync(string queueId, CancellationToken cancellationToken = default);
}
