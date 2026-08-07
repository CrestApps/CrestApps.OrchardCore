using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Selects the next queue item to route, optionally aging waiting items past the SLA threshold so that older
/// work is routed ahead of newer higher-priority work.
/// </summary>
public static class QueueItemPrioritizer
{
    /// <summary>
    /// Selects the highest-priority waiting item, applying SLA aging when the queue enables it.
    /// </summary>
    /// <param name="waiting">The waiting items to choose from.</param>
    /// <param name="queue">The queue whose policy controls aging.</param>
    /// <param name="utcNow">The current UTC instant used to measure wait time.</param>
    /// <returns>The next item to route, or <see langword="null"/> when there are none.</returns>
    public static QueueItem SelectNext(IEnumerable<QueueItem> waiting, ActivityQueue queue, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(queue);

        if (waiting is null)
        {
            return null;
        }

        return waiting
            .OrderByDescending(item => GetEffectivePriority(item, queue, utcNow))
            .ThenBy(item => item.EnqueuedUtc)
            .FirstOrDefault();
    }

    /// <summary>
    /// Calculates the effective routing priority of an item, including any SLA-aging bonus.
    /// </summary>
    /// <param name="item">The queue item to score.</param>
    /// <param name="queue">The queue whose policy controls aging.</param>
    /// <param name="utcNow">The current UTC instant used to measure wait time.</param>
    /// <returns>The effective priority where higher values are routed first.</returns>
    public static int GetEffectivePriority(QueueItem item, ActivityQueue queue, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(queue);

        var basePriority = (int)item.Priority;

        if (!queue.EnableSlaAging || queue.SlaThresholdSeconds <= 0)
        {
            return basePriority;
        }

        var waitSeconds = (utcNow - item.EnqueuedUtc).TotalSeconds;
        var overdueSeconds = waitSeconds - queue.SlaThresholdSeconds;

        if (overdueSeconds <= 0)
        {
            return basePriority;
        }

        var agingSteps = (int)Math.Floor(overdueSeconds / queue.SlaThresholdSeconds);

        return basePriority + agingSteps;
    }
}
