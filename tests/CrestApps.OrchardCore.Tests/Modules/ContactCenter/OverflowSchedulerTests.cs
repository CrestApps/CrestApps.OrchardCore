using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Overflow was a single hop evaluated by a once-a-minute sweep, so a queue configured to overflow after twenty
/// seconds actually overflowed somewhere between sixty and eighty. The due time is now computed when the item is
/// enqueued and read by whatever touches the item next, so the threshold means what it says.
/// </summary>
public sealed class OverflowSchedulerTests
{
    private static readonly DateTime _now = new(2026, 3, 4, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NextTarget_IsNotDue_BeforeTheThreshold()
    {
        // Arrange
        var queue = Queue(("tier2", 20));
        var item = Item(enqueuedUtc: _now.AddSeconds(-10));

        // Act
        var target = OverflowScheduler.SelectNextTarget(item, queue, _now);

        // Assert
        Assert.Null(target);
    }

    [Fact]
    public void NextTarget_IsDue_OnceTheThresholdPasses()
    {
        // Arrange
        var queue = Queue(("tier2", 20));
        var item = Item(enqueuedUtc: _now.AddSeconds(-21));

        // Act
        var target = OverflowScheduler.SelectNextTarget(item, queue, _now);

        // Assert
        Assert.Equal("tier2", target);
    }

    [Fact]
    public void NextTarget_TakesTheFurthestHopWhoseThresholdHasPassed()
    {
        // Arrange
        // A caller who has already waited past every tier should land at the last one, not crawl through each in
        // turn while the sweep ticks.
        var queue = Queue(("tier2", 20), ("tier3", 60));
        var item = Item(enqueuedUtc: _now.AddSeconds(-90));

        // Act
        var target = OverflowScheduler.SelectNextTarget(item, queue, _now);

        // Assert
        Assert.Equal("tier3", target);
    }

    [Fact]
    public void NextTarget_SkipsAQueueTheItemHasAlreadyVisited()
    {
        // Arrange
        // Two queues configured to overflow into each other would otherwise pass the caller back and forth
        // forever, and each hop resets nothing, so the caller never reaches anybody.
        var queue = Queue(("tier2", 20), ("tier3", 60));
        var item = Item(enqueuedUtc: _now.AddSeconds(-90));
        item.OverflowHistory.Add("tier3");

        // Act
        var target = OverflowScheduler.SelectNextTarget(item, queue, _now);

        // Assert
        Assert.Equal("tier2", target);
    }

    [Fact]
    public void NextTarget_IsNothing_WhenEveryHopHasBeenVisited()
    {
        // Arrange
        var queue = Queue(("tier2", 20));
        var item = Item(enqueuedUtc: _now.AddSeconds(-90));
        item.OverflowHistory.Add("tier2");

        // Act
        var target = OverflowScheduler.SelectNextTarget(item, queue, _now);

        // Assert
        Assert.Null(target);
    }

    [Fact]
    public void NextTarget_NeverReturnsTheQueueTheItemIsAlreadyIn()
    {
        // Arrange
        // A queue that lists itself as an overflow target is a misconfiguration, and honouring it is an infinite
        // loop that reads as a caller who is never answered.
        var queue = Queue(("q1", 20));
        var item = Item(enqueuedUtc: _now.AddSeconds(-90));

        // Act
        var target = OverflowScheduler.SelectNextTarget(item, queue, _now);

        // Assert
        Assert.Null(target);
    }

    [Fact]
    public void LegacySingleHop_IsStillHonoured()
    {
        // Arrange
        // Existing queues configured through the old single-target fields must keep overflowing; dropping them
        // silently would strand every caller those queues were built to hand on.
        var queue = new ActivityQueue { ItemId = "q1", OverflowQueueId = "tier2", OverflowAfterSeconds = 20 };
        var item = Item(enqueuedUtc: _now.AddSeconds(-30));

        // Act
        var target = OverflowScheduler.SelectNextTarget(item, queue, _now);

        // Assert
        Assert.Equal("tier2", target);
    }

    [Fact]
    public void DueUtc_IsTheEarliestUnvisitedThreshold_SoASchedulerKnowsWhenToLookAgain()
    {
        // Arrange
        var queue = Queue(("tier2", 20), ("tier3", 60));
        var item = Item(enqueuedUtc: _now);

        // Act
        var dueUtc = OverflowScheduler.GetNextDueUtc(item, queue);

        // Assert
        Assert.Equal(_now.AddSeconds(20), dueUtc);
    }

    [Fact]
    public void DueUtc_IsNothing_WhenNoHopRemains()
    {
        // Arrange
        var queue = new ActivityQueue { ItemId = "q1" };
        var item = Item(enqueuedUtc: _now);

        // Act
        var dueUtc = OverflowScheduler.GetNextDueUtc(item, queue);

        // Assert
        Assert.Null(dueUtc);
    }

    private static ActivityQueue Queue(params (string QueueId, int AfterSeconds)[] targets)
    {
        var queue = new ActivityQueue { ItemId = "q1" };

        foreach (var (queueId, afterSeconds) in targets)
        {
            queue.OverflowTargets.Add(new QueueOverflowTarget { QueueId = queueId, AfterSeconds = afterSeconds });
        }

        return queue;
    }

    private static QueueItem Item(DateTime enqueuedUtc)
        => new() { ItemId = "i1", QueueId = "q1", EnqueuedUtc = enqueuedUtc, QueueEnteredUtc = enqueuedUtc };
}
