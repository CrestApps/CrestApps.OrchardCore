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
    Task<IReadOnlyCollection<QueueItem>> ListWaitingAsync(string queueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the active queue item for the specified activity.
    /// </summary>
    /// <param name="activityItemId">The CRM activity identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching queue item, or <see langword="null"/> when none exists.</returns>
    Task<QueueItem> FindByActivityIdAsync(string activityItemId, CancellationToken cancellationToken = default);
}
