using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Plays whatever the queue's treatment policy says is due to the callers waiting in it.
/// </summary>
public interface IQueueTreatmentService
{
    /// <summary>
    /// Runs one treatment pass over a queue's waiting callers.
    /// </summary>
    /// <param name="queue">The queue.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many callers heard something.</returns>
    Task<int> RunDueAsync(ActivityQueue queue, CancellationToken cancellationToken = default);
}
