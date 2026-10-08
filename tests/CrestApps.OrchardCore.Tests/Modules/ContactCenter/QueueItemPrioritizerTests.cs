using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class QueueItemPrioritizerTests
{
    private static readonly DateTime _now = new(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SelectNext_WithoutAging_PrefersHighestPriority()
    {
        // Arrange
        var queue = new ActivityQueue { ItemId = "q1" };
        var older = new QueueItem { ItemId = "old", Priority = InteractionPriority.Normal, EnqueuedUtc = _now.AddMinutes(-10) };
        var higher = new QueueItem { ItemId = "high", Priority = InteractionPriority.High, EnqueuedUtc = _now };

        // Act
        var selected = QueueItemPrioritizer.SelectNext([older, higher], queue, _now);

        // Assert
        Assert.Same(higher, selected);
    }

    [Fact]
    public void SelectNext_WithoutAging_PrefersOldestWithinSamePriority()
    {
        // Arrange
        var queue = new ActivityQueue { ItemId = "q1" };
        var newer = new QueueItem { ItemId = "newer", Priority = InteractionPriority.Normal, EnqueuedUtc = _now.AddMinutes(-1) };
        var older = new QueueItem { ItemId = "older", Priority = InteractionPriority.Normal, EnqueuedUtc = _now.AddMinutes(-5) };

        // Act
        var selected = QueueItemPrioritizer.SelectNext([newer, older], queue, _now);

        // Assert
        Assert.Same(older, selected);
    }

    [Fact]
    public void SelectNext_WithAging_AgedLowPriorityBeatsNewerHigherPriority()
    {
        // Arrange
        var queue = new ActivityQueue { ItemId = "q1", EnableSlaAging = true, SlaThresholdSeconds = 60 };
        var agedLow = new QueueItem { ItemId = "aged", Priority = InteractionPriority.Lowest, EnqueuedUtc = _now.AddSeconds(-200) };
        var freshHigher = new QueueItem { ItemId = "fresh", Priority = InteractionPriority.Low, EnqueuedUtc = _now };

        // Act
        var withAging = QueueItemPrioritizer.SelectNext([agedLow, freshHigher], queue, _now);
        var withoutAging = QueueItemPrioritizer.SelectNext([agedLow, freshHigher], new ActivityQueue { ItemId = "q1" }, _now);

        // Assert
        Assert.Same(agedLow, withAging);
        Assert.Same(freshHigher, withoutAging);
    }

    [Fact]
    public void GetEffectivePriority_WithAging_AddsOneStepPerSlaInterval()
    {
        // Arrange
        var queue = new ActivityQueue { ItemId = "q1", EnableSlaAging = true, SlaThresholdSeconds = 60 };
        var item = new QueueItem { Priority = InteractionPriority.Normal, EnqueuedUtc = _now.AddSeconds(-200) };

        // Act
        var effective = QueueItemPrioritizer.GetEffectivePriority(item, queue, _now);

        // Assert
        // Normal (2) + floor((200 - 60) / 60) = 2 + 2 = 4.
        Assert.Equal(4, effective);
    }
}
