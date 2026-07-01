using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ActivityQueueServiceTests
{
    private static readonly DateTime _now = new(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task EnqueueAsync_CapturesAssignedUserAsStickyHint()
    {
        // Arrange
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.FindByActivityIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync((QueueItem)null);
        queueItemManager.Setup(m => m.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>())).ReturnsAsync(new QueueItem());

        var queueManager = new Mock<IActivityQueueManager>();
        queueManager.Setup(m => m.FindByIdAsync("q1", It.IsAny<CancellationToken>())).ReturnsAsync(new ActivityQueue { ItemId = "q1" });

        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.FindByIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(new OmnichannelActivity { ItemId = "act-1", AssignedToId = "user-7" });

        var service = CreateService(queueItemManager, queueManager, activityManager, new Mock<IBusinessHoursService>());

        // Act
        var item = await service.EnqueueAsync("act-1", "q1", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("user-7", item.StickyAgentUserId);
        Assert.Equal("q1", item.QueueId);
        Assert.Equal(QueueItemStatus.Waiting, item.Status);
    }

    [Fact]
    public async Task OverflowDueAsync_WhenNoOverflowTarget_ReturnsZero()
    {
        // Arrange
        var queueItemManager = new Mock<IQueueItemManager>();
        var service = CreateService(queueItemManager, new Mock<IActivityQueueManager>(), new Mock<IOmnichannelActivityManager>(), new Mock<IBusinessHoursService>());
        var queue = new ActivityQueue { ItemId = "q1", OverflowQueueId = null };

        // Act
        var moved = await service.OverflowDueAsync(queue, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, moved);
        queueItemManager.Verify(m => m.ListWaitingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OverflowDueAsync_MovesOnlyItemsWaitingPastThreshold()
    {
        // Arrange
        var overdue = new QueueItem { ItemId = "i1", QueueId = "q1", Status = QueueItemStatus.Waiting, EnqueuedUtc = _now.AddSeconds(-60) };
        var fresh = new QueueItem { ItemId = "i2", QueueId = "q1", Status = QueueItemStatus.Waiting, EnqueuedUtc = _now.AddSeconds(-10) };

        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.ListWaitingAsync("q1", It.IsAny<CancellationToken>())).ReturnsAsync([overdue, fresh]);

        var service = CreateService(queueItemManager, new Mock<IActivityQueueManager>(), new Mock<IOmnichannelActivityManager>(), new Mock<IBusinessHoursService>());
        var queue = new ActivityQueue { ItemId = "q1", OverflowQueueId = "q2", OverflowAfterSeconds = 30 };

        // Act
        var moved = await service.OverflowDueAsync(queue, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, moved);
        Assert.Equal("q2", overdue.QueueId);
        Assert.Equal("q1", overdue.OverflowedFromQueueId);
        Assert.Equal("q1", fresh.QueueId);
    }

    [Fact]
    public async Task OverflowDueAsync_WhenClosedAndAfterHoursOverflow_MovesAllWaitingItems()
    {
        // Arrange
        var item1 = new QueueItem { ItemId = "i1", QueueId = "q1", Status = QueueItemStatus.Waiting, EnqueuedUtc = _now.AddSeconds(-10) };
        var item2 = new QueueItem { ItemId = "i2", QueueId = "q1", Status = QueueItemStatus.Waiting, EnqueuedUtc = _now.AddSeconds(-5) };

        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.ListWaitingAsync("q1", It.IsAny<CancellationToken>())).ReturnsAsync([item1, item2]);

        var businessHours = new Mock<IBusinessHoursService>();
        businessHours.Setup(b => b.IsOpenAsync("cal", It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var service = CreateService(queueItemManager, new Mock<IActivityQueueManager>(), new Mock<IOmnichannelActivityManager>(), businessHours);
        var queue = new ActivityQueue
        {
            ItemId = "q1",
            OverflowQueueId = "q2",
            OverflowAfterSeconds = 0,
            BusinessHoursCalendarId = "cal",
            AfterHoursAction = QueueAfterHoursAction.Overflow,
        };

        // Act
        var moved = await service.OverflowDueAsync(queue, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, moved);
        Assert.Equal("q2", item1.QueueId);
        Assert.Equal("q2", item2.QueueId);
    }

    private static ActivityQueueService CreateService(
        Mock<IQueueItemManager> queueItemManager,
        Mock<IActivityQueueManager> queueManager,
        Mock<IOmnichannelActivityManager> activityManager,
        Mock<IBusinessHoursService> businessHours)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new ActivityQueueService(
            queueItemManager.Object,
            queueManager.Object,
            activityManager.Object,
            businessHours.Object,
            new Mock<IContactCenterEventPublisher>().Object,
            clock.Object);
    }
}
