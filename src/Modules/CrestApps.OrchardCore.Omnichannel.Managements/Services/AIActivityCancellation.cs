using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Cancels an automated AI call or text that has not started yet, for the AI activities API and the AI tool that
/// share the rule.
/// </summary>
internal static class AIActivityCancellation
{
    /// <summary>
    /// Whether an activity in this status has not started, so it can still be cancelled.
    /// </summary>
    /// <param name="status">The activity's status.</param>
    /// <returns><see langword="true"/> while the activity is not started, pending or scheduled.</returns>
    public static bool CanCancel(ActivityStatus status)
        => status is ActivityStatus.NotStated or ActivityStatus.Pending or ActivityStatus.Scheduled;

    /// <summary>
    /// Cancels the activity when it has not started.
    /// </summary>
    /// <param name="activityManager">The activity manager the activity is saved with.</param>
    /// <param name="clock">The clock the completion time is read from.</param>
    /// <param name="activity">The activity.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="false"/> when the activity already started or finished and was left as it is.</returns>
    public static async Task<bool> CancelAsync(
        IOmnichannelActivityManager activityManager,
        IClock clock,
        OmnichannelActivity activity,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(activityManager);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(activity);

        if (!CanCancel(activity.Status))
        {
            return false;
        }

        activity.Status = ActivityStatus.Cancelled;
        activity.CompletedUtc = clock.UtcNow;
        await activityManager.UpdateAsync(activity, cancellationToken: cancellationToken);

        return true;
    }
}
