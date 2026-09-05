using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Decides where a waiting caller overflows to and when the decision is next worth making. Overflow used to be a
/// single hop evaluated by a once-a-minute sweep, so a queue configured to overflow after twenty seconds
/// actually overflowed somewhere between sixty and eighty.
/// </summary>
public static class OverflowScheduler
{
    /// <summary>
    /// Selects the queue this item should overflow to now, or <see langword="null"/> when none applies.
    /// </summary>
    /// <param name="item">The waiting item.</param>
    /// <param name="queue">The queue the item is in.</param>
    /// <param name="nowUtc">The current instant.</param>
    public static string SelectNextTarget(QueueItem item, ActivityQueue queue, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(queue);

        var waitedSeconds = (nowUtc - item.QueueEnteredUtc).TotalSeconds;

        // Furthest hop first: a caller who has already waited past every tier belongs at the last one, not
        // crawling through each in turn while a sweep ticks.
        foreach (var target in GetTargets(queue).OrderByDescending(target => target.AfterSeconds))
        {
            if (waitedSeconds >= target.AfterSeconds && IsEligible(item, queue, target.QueueId))
            {
                return target.QueueId;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether this queue hands callers on at all. A queue that names nowhere to go is not worth reading every
    /// waiting caller out of the database for.
    /// </summary>
    /// <param name="queue">The queue.</param>
    public static bool HasAnyTarget(ActivityQueue queue)
    {
        ArgumentNullException.ThrowIfNull(queue);

        return GetTargets(queue).Length > 0;
    }

    /// <summary>
    /// Selects the first hop this item has not already been through, ignoring how long they have waited.
    /// </summary>
    /// <remarks>
    /// For the closed-hours case: the wait threshold exists to give the queue a chance to answer, and a closed
    /// queue is not going to, so making the caller sit out the timer first only delays them.
    /// </remarks>
    /// <param name="item">The waiting item.</param>
    /// <param name="queue">The queue the item is in.</param>
    public static string SelectFirstEligibleTarget(QueueItem item, ActivityQueue queue)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(queue);

        return GetTargets(queue)
            .OrderBy(target => target.AfterSeconds)
            .FirstOrDefault(target => IsEligible(item, queue, target.QueueId))
            ?.QueueId;
    }

    /// <summary>
    /// Returns when this item's next overflow hop becomes due, so a scheduler knows when to look again rather
    /// than polling every item every minute.
    /// </summary>
    /// <param name="item">The waiting item.</param>
    /// <param name="queue">The queue the item is in.</param>
    public static DateTime? GetNextDueUtc(QueueItem item, ActivityQueue queue)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(queue);

        var next = GetTargets(queue)
            .Where(target => IsEligible(item, queue, target.QueueId))
            .OrderBy(target => target.AfterSeconds)
            .FirstOrDefault();

        return next is null
            ? null
            : item.QueueEnteredUtc.AddSeconds(next.AfterSeconds);
    }

    /// <summary>
    /// Reads the overflow chain, falling back to the single-target fields for queues configured before chains
    /// existed. Dropping those silently would strand every caller those queues were built to hand on.
    /// </summary>
    private static QueueOverflowTarget[] GetTargets(ActivityQueue queue)
    {
        var targets = queue.OverflowTargets
            .Where(target => !string.IsNullOrWhiteSpace(target?.QueueId))
            .ToArray();

        if (targets.Length > 0)
        {
            return targets;
        }

        return string.IsNullOrWhiteSpace(queue.OverflowQueueId)
            ? []
            : [new QueueOverflowTarget { QueueId = queue.OverflowQueueId, AfterSeconds = queue.OverflowAfterSeconds }];
    }

    /// <summary>
    /// A target the item has already visited, or the queue it is already in, would pass the caller in a circle
    /// while resetting nothing, so they would never reach anybody.
    /// </summary>
    private static bool IsEligible(QueueItem item, ActivityQueue queue, string targetQueueId)
        => !string.Equals(targetQueueId, queue.ItemId, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(targetQueueId, item.QueueId, StringComparison.OrdinalIgnoreCase)
            && !item.OverflowHistory.Contains(targetQueueId, StringComparer.OrdinalIgnoreCase);
}
