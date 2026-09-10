using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Schedules callbacks and promotes due callbacks into outbound CRM activities.
/// </summary>
public interface ICallbackService
{
    /// <summary>
    /// Schedules a callback for a future time and records it as pending.
    /// </summary>
    /// <param name="callback">The callback to schedule.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The scheduled callback.</returns>
    Task<CallbackRequest> ScheduleAsync(CallbackRequest callback, CancellationToken cancellationToken = default);

    /// <summary>
    /// Promotes every pending callback that is due into an outbound CRM activity and, when a queue is set,
    /// enqueues it for routing.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of callbacks promoted.</returns>
    Task<int> PromoteDueAsync(CancellationToken cancellationToken = default);
}
