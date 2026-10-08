using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Takes an activity's work out of the queue once the activity is no longer routable, so routing never offers an
/// agent an activity that was purged, cancelled, completed, failed or deleted outside routing.
/// </summary>
public interface IQueuedWorkWithdrawalService
{
    /// <summary>
    /// Withdraws the activity's queued work. Waiting work is removed; work ringing an agent has the offer revoked
    /// and the agent released; work an agent has already taken is left with them.
    /// </summary>
    /// <param name="activityItemId">The activity that stopped being routable.</param>
    /// <param name="reason">What happened to the activity, recorded as why the work left.</param>
    /// <param name="actor">Who changed the activity.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>What was done.</returns>
    Task<QueuedWorkWithdrawalOutcome> WithdrawAsync(
        string activityItemId,
        string reason,
        ContactCenterActor actor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Withdraws a waiting queue item whose activity is no longer routable. Routing calls this for the item it is
    /// about to offer, while it already holds the item's queue, so a queue item left behind by an activity that
    /// ended out of band heals on the next pass instead of being offered.
    /// </summary>
    /// <param name="queueItem">The waiting queue item routing picked.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the item was withdrawn and must not be offered.</returns>
    Task<bool> TryWithdrawUnroutableAsync(QueueItem queueItem, CancellationToken cancellationToken = default);
}
