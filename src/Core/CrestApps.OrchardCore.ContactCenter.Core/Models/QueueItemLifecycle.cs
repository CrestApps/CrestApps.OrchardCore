using System.Collections.Frozen;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Declares which queue-item status changes the domain admits.
/// <para>
/// A queue item can legitimately go backwards — a reservation that expires returns the item to the queue, and an
/// assignment that fails re-queues it — so this lifecycle cannot be expressed as a rank at all. What it must
/// refuse is an item leaving a settled status: an item that was completed and then re-enters the queue is handed
/// to a second agent for work that is already done.
/// </para>
/// </summary>
public static class QueueItemLifecycle
{
    private static readonly FrozenDictionary<QueueItemStatus, FrozenSet<QueueItemStatus>> _transitions =
        new Dictionary<QueueItemStatus, FrozenSet<QueueItemStatus>>
        {
            [QueueItemStatus.Waiting] = FrozenSet.ToFrozenSet(
            [
                QueueItemStatus.Reserved,
                QueueItemStatus.Assigned,
                QueueItemStatus.Completed,
                QueueItemStatus.Removed,
            ]),

            // A reservation that is rejected, expires, or is cancelled returns the item to the queue for the
            // next agent, which is why Reserved reaches Waiting.
            [QueueItemStatus.Reserved] = FrozenSet.ToFrozenSet(
            [
                QueueItemStatus.Waiting,
                QueueItemStatus.Assigned,
                QueueItemStatus.Completed,
                QueueItemStatus.Removed,
            ]),

            // An agent whose client disappears mid-assignment releases the item back to the queue rather than
            // stranding it, so Assigned reaches Waiting too.
            [QueueItemStatus.Assigned] = FrozenSet.ToFrozenSet(
            [
                QueueItemStatus.Waiting,
                QueueItemStatus.Completed,
                QueueItemStatus.Removed,
            ]),
            [QueueItemStatus.Completed] = FrozenSet<QueueItemStatus>.Empty,
            [QueueItemStatus.Removed] = FrozenSet<QueueItemStatus>.Empty,
        }.ToFrozenDictionary();

    /// <summary>
    /// Determines whether a queue item in one status may move to another.
    /// </summary>
    /// <param name="from">The status the item is in.</param>
    /// <param name="to">The status the item would move to.</param>
    /// <returns><see langword="true"/> when the transition is admitted; otherwise <see langword="false"/>.</returns>
    public static bool CanTransition(QueueItemStatus from, QueueItemStatus to)
    {
        if (from == to)
        {
            return true;
        }

        return _transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }

    /// <summary>
    /// Determines whether a queue-item status is settled, meaning the item has left the queue for good.
    /// </summary>
    /// <param name="status">The status to inspect.</param>
    /// <returns><see langword="true"/> when the status is settled; otherwise <see langword="false"/>.</returns>
    public static bool IsSettled(QueueItemStatus status)
        => status == QueueItemStatus.Completed || status == QueueItemStatus.Removed;
}
