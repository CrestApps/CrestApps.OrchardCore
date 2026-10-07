using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Reads the next waiting item of a queue that may be worked now: the head of the queue, after withdrawing any item ahead
/// of it whose activity was purged, closed or deleted outside routing, and skipping a campaign record that may not be
/// dialed yet. Routing reserves an agent for the item it returns, and over-dialing places its call without one; both read
/// the queue the same way.
/// </summary>
internal static class RoutableQueueHead
{
    /// <summary>
    /// The most items one read withdraws before giving up for this pass.
    /// </summary>
    public const int MaxWithdrawalsPerPass = 50;

    /// <summary>
    /// Finds the next waiting item whose activity is still routable and may be worked now.
    /// </summary>
    /// <param name="queueItemManager">The queue item manager.</param>
    /// <param name="withdrawalService">The service that withdraws an item whose activity is no longer routable.</param>
    /// <param name="dialerWorkGate">The gate that holds back a campaign record that may not be dialed yet, or
    /// <see langword="null"/> when every waiting record may be worked.</param>
    /// <param name="queue">The queue.</param>
    /// <param name="now">The current time.</param>
    /// <param name="heldBack">The items already held back in this pass; reaching one again means none is due.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The item, or <see langword="null"/> when nothing in the queue may be worked now.</returns>
    public static async Task<QueueItem> NextAsync(
        IQueueItemManager queueItemManager,
        IQueuedWorkWithdrawalService withdrawalService,
        IQueuedDialerWorkGate dialerWorkGate,
        ActivityQueue queue,
        DateTime now,
        HashSet<string> heldBack,
        CancellationToken cancellationToken)
    {
        for (var withdrawn = 0; withdrawn < MaxWithdrawalsPerPass; withdrawn++)
        {
            var item = await queueItemManager.FindNextWaitingAsync(queue, now, cancellationToken);

            if (item is null)
            {
                return null;
            }

            if (await withdrawalService.TryWithdrawUnroutableAsync(item, cancellationToken))
            {
                continue;
            }

            if (dialerWorkGate is null)
            {
                return item;
            }

            // A record held back goes to the back of the queue. Reaching one again in the same pass means every
            // waiting record has been looked at and none is due.
            if (heldBack.Contains(item.ItemId))
            {
                return null;
            }

            if (!await dialerWorkGate.TryHoldBackAsync(item, now, cancellationToken))
            {
                return item;
            }

            heldBack.Add(item.ItemId);
        }

        // A backlog of dead items longer than one pass is withdrawn over the following passes.
        return null;
    }
}
