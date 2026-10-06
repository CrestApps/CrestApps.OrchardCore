using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Schedules the next attempt of a completed dialer campaign record, for a workflow that decides when to call again.
/// </summary>
public interface IDialerRetryScheduler
{
    /// <summary>
    /// Creates the next attempt of the completed activity and queues it in the campaign it was dialed from, due after
    /// the delay and never sooner than the dialer profile's retry delay. No attempt is created past the profile's limit.
    /// </summary>
    /// <param name="activityItemId">The completed dialer activity to try again.</param>
    /// <param name="delayMinutes">How long to wait before the next attempt is dialed; the profile's retry delay when null.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>What became of the request, and the next attempt when one was created.</returns>
    Task<DialerRetryScheduleResult> ScheduleRetryAsync(string activityItemId, int? delayMinutes, CancellationToken cancellationToken = default);
}
