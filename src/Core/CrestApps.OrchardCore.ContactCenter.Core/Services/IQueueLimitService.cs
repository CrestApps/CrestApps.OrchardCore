using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Enforces the two limits a queue can set on its callers: how many may wait at once, and how long any of them
/// may wait.
/// </summary>
public interface IQueueLimitService
{
    /// <summary>
    /// Decides whether a new caller may wait in the queue, and where they go when it is full.
    /// </summary>
    /// <param name="queue">The queue the caller is routed to.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The admission decision.</returns>
    Task<QueueAdmissionDecision> AdmitAsync(ActivityQueue queue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the queue's maximum-wait action to every caller who has waited past it.
    /// </summary>
    /// <param name="queue">The queue whose waiting callers are evaluated.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of callers the action was applied to.</returns>
    Task<int> EnforceMaxWaitAsync(ActivityQueue queue, CancellationToken cancellationToken = default);
}
