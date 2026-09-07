using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Moq;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ActivityQueueServiceTests
{
    private static Mock<IQueueTreatmentProvider> TreatmentProvider { get; set; }

    private static Mock<IInteractionManager> InteractionManagerForDequeue { get; set; }

    private static readonly DateTime _now = new(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task LeavingTheQueue_StopsTheHoldMusic()
    {
        // Arrange
        // Hold music is started on an infinite loop and nothing ever stopped it. Whoever the caller went to next
        // was talking underneath it — an agent who answered, the voicemail greeting, the next queue in the
        // overflow chain — and a caller whose call was torn down heard it play on into a finished call. Observed
        // live: a caller sent to voicemail at sixty seconds was still hearing music minutes later.
        var queueItemManager = new Mock<IQueueItemManager>();
        var service = CreateService(queueItemManager, new Mock<IActivityQueueManager>(), new Mock<IOmnichannelActivityManager>(), new Mock<IBusinessHoursService>());

        var item = new QueueItem
        {
            ItemId = "qi-1",
            QueueId = "q1",
            ActivityItemId = "act-1",
        };

        // Act
        await service.DequeueAsync(item, QueueItemStatus.Removed, TestContext.Current.CancellationToken);

        // Assert
        TreatmentProvider.Verify(x => x.StopHoldMusicAsync("ctrl-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ACallerWhoWasNotWaiting_DoesNotHaveAnythingStopped()
    {
        // Arrange
        // Only a waiting caller can have hold music playing. Issuing a stop for a reserved or assigned item would
        // cut across whatever the agent's leg is doing.
        var queueItemManager = new Mock<IQueueItemManager>();
        var service = CreateService(queueItemManager, new Mock<IActivityQueueManager>(), new Mock<IOmnichannelActivityManager>(), new Mock<IBusinessHoursService>());

        var item = new QueueItem
        {
            ItemId = "qi-1",
            QueueId = "q1",
            ActivityItemId = "act-1",
        };

        item.TransitionTo(QueueItemStatus.Reserved);
        item.TransitionTo(QueueItemStatus.Assigned);

        // Act
        await service.DequeueAsync(item, QueueItemStatus.Completed, TestContext.Current.CancellationToken);

        // Assert
        TreatmentProvider.Verify(x => x.StopHoldMusicAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AProviderThatRefusesTheStop_DoesNotStrandTheCallerInTheQueue()
    {
        // Arrange
        // The caller has already left; failing the dequeue over the music would put them back in a queue nobody
        // is going to answer.
        var queueItemManager = new Mock<IQueueItemManager>();
        var service = CreateService(queueItemManager, new Mock<IActivityQueueManager>(), new Mock<IOmnichannelActivityManager>(), new Mock<IBusinessHoursService>());

        TreatmentProvider
            .Setup(x => x.StopHoldMusicAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The leg has already gone."));

        var item = new QueueItem
        {
            ItemId = "qi-1",
            QueueId = "q1",
            ActivityItemId = "act-1",
        };

        // Act
        await service.DequeueAsync(item, QueueItemStatus.Removed, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(QueueItemStatus.Removed, item.Status);
    }

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
        Assert.Equal(_now, item.EnqueuedUtc);
        Assert.Equal(_now, item.QueueEnteredUtc);
    }

    [Fact]
    public async Task EnqueueAsync_WhenPriorityIsNotProvided_UsesQueueDefaultPriority()
    {
        // Arrange
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.FindByActivityIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync((QueueItem)null);
        queueItemManager.Setup(m => m.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>())).ReturnsAsync(new QueueItem());

        var queueManager = new Mock<IActivityQueueManager>();
        queueManager.Setup(m => m.FindByIdAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityQueue { ItemId = "q1", DefaultPriority = InteractionPriority.High });

        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.FindByIdAsync("act-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelActivity { ItemId = "act-1" });

        var service = CreateService(queueItemManager, queueManager, activityManager, new Mock<IBusinessHoursService>());

        // Act
        var item = await service.EnqueueAsync("act-1", "q1", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionPriority.High, item.Priority);
    }

    [Fact]
    public async Task EnqueueAsync_FlushesQueuedWorkBeforePublishingTheQueueEvent()
    {
        // Arrange
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.FindByActivityIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync((QueueItem)null);
        queueItemManager.Setup(m => m.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>())).ReturnsAsync(new QueueItem());

        var queueManager = new Mock<IActivityQueueManager>();
        queueManager.Setup(m => m.FindByIdAsync("q1", It.IsAny<CancellationToken>())).ReturnsAsync(new ActivityQueue { ItemId = "q1" });

        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.FindByIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(new OmnichannelActivity { ItemId = "act-1" });

        var session = new Mock<ISession>();
        var publisher = new Mock<IContactCenterEventPublisher>();
        var sequence = new MockSequence();
        session.InSequence(sequence)
            .Setup(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        publisher.InSequence(sequence)
            .Setup(p => p.PublishAsync(
                It.Is<InteractionEvent>(interactionEvent => interactionEvent.EventType == ContactCenterConstants.Events.QueueItemAdded),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(
            queueItemManager,
            queueManager,
            activityManager,
            new Mock<IBusinessHoursService>(),
            session,
            publisher);

        // Act
        await service.EnqueueAsync("act-1", "q1", null, TestContext.Current.CancellationToken);

        // Assert
        session.Verify(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        publisher.VerifyAll();
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
        queueItemManager.Verify(m => m.GetWaitingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OverflowDueAsync_MovesOnlyItemsWaitingPastThreshold()
    {
        // Arrange
        var overdue = new QueueItem { ItemId = "i1", QueueId = "q1", EnqueuedUtc = _now.AddSeconds(-60) }.RestorePersistedStatus(QueueItemStatus.Waiting);
        var fresh = new QueueItem { ItemId = "i2", QueueId = "q1", EnqueuedUtc = _now.AddSeconds(-10) }.RestorePersistedStatus(QueueItemStatus.Waiting);

        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.GetWaitingAsync("q1", It.IsAny<CancellationToken>())).ReturnsAsync([overdue, fresh]);

        var service = CreateService(queueItemManager, new Mock<IActivityQueueManager>(), new Mock<IOmnichannelActivityManager>(), new Mock<IBusinessHoursService>());
        var queue = new ActivityQueue { ItemId = "q1", OverflowQueueId = "q2", OverflowAfterSeconds = 30 };

        // Act
        var moved = await service.OverflowDueAsync(queue, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, moved);
        Assert.Equal("q2", overdue.QueueId);
        Assert.Equal("q1", overdue.OverflowedFromQueueId);
        Assert.Equal(_now.AddSeconds(-60), overdue.EnqueuedUtc);
        Assert.Equal(_now, overdue.QueueEnteredUtc);
        Assert.Contains("q1", overdue.OverflowHistory);
        Assert.Equal("q1", fresh.QueueId);
    }

    [Fact]
    public async Task OverflowDueAsync_FollowsTheConfiguredChain_NotJustTheFirstHop()
    {
        // Arrange
        // A queue can name several tiers to fall through. Only the legacy single-target field was ever read, so
        // every tier past the first was configuration the product ignored.
        var item = new QueueItem { ItemId = "i1", QueueId = "q1", QueueEnteredUtc = _now.AddSeconds(-40) }
            .RestorePersistedStatus(QueueItemStatus.Waiting);
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.GetWaitingAsync("q1", It.IsAny<CancellationToken>())).ReturnsAsync([item]);

        var service = CreateService(queueItemManager, new Mock<IActivityQueueManager>(), new Mock<IOmnichannelActivityManager>(), new Mock<IBusinessHoursService>());
        var queue = new ActivityQueue
        {
            ItemId = "q1",
            OverflowTargets =
            [
                new QueueOverflowTarget { QueueId = "tier-1", AfterSeconds = 20 },
                new QueueOverflowTarget { QueueId = "tier-2", AfterSeconds = 35 },
            ],
        };

        // Act
        var moved = await service.OverflowDueAsync(queue, TestContext.Current.CancellationToken);

        // Assert
        // Furthest hop first: somebody who has already waited past every tier belongs at the last one, not
        // crawling through each in turn while a sweep ticks.
        Assert.Equal(1, moved);
        Assert.Equal("tier-2", item.QueueId);
    }

    [Fact]
    public async Task OverflowDueAsync_DoesNotSendACallerSomewhereTheyHaveAlreadyBeen()
    {
        // Arrange
        // A caller passed in a circle waits forever while their wait resets at every hop.
        var item = new QueueItem
        {
            ItemId = "i1",
            QueueId = "q1",
            QueueEnteredUtc = _now.AddSeconds(-60),
            OverflowHistory = ["tier-1"],
        }.RestorePersistedStatus(QueueItemStatus.Waiting);
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.GetWaitingAsync("q1", It.IsAny<CancellationToken>())).ReturnsAsync([item]);

        var service = CreateService(queueItemManager, new Mock<IActivityQueueManager>(), new Mock<IOmnichannelActivityManager>(), new Mock<IBusinessHoursService>());
        var queue = new ActivityQueue
        {
            ItemId = "q1",
            OverflowTargets = [new QueueOverflowTarget { QueueId = "tier-1", AfterSeconds = 20 }],
        };

        // Act
        var moved = await service.OverflowDueAsync(queue, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, moved);
        Assert.Equal("q1", item.QueueId);
    }

    [Fact]
    public async Task OverflowDueAsync_StillHonoursAQueueConfiguredBeforeChainsExisted()
    {
        // Arrange
        // Dropping the single-target field would strand every caller the queues already in production were
        // built to hand on.
        var item = new QueueItem { ItemId = "i1", QueueId = "q1", QueueEnteredUtc = _now.AddSeconds(-60) }
            .RestorePersistedStatus(QueueItemStatus.Waiting);
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.GetWaitingAsync("q1", It.IsAny<CancellationToken>())).ReturnsAsync([item]);

        var service = CreateService(queueItemManager, new Mock<IActivityQueueManager>(), new Mock<IOmnichannelActivityManager>(), new Mock<IBusinessHoursService>());
        var queue = new ActivityQueue { ItemId = "q1", OverflowQueueId = "q2", OverflowAfterSeconds = 30 };

        // Act
        var moved = await service.OverflowDueAsync(queue, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, moved);
        Assert.Equal("q2", item.QueueId);
    }

    [Fact]
    public async Task OverflowDueAsync_WhenItemRecentlyEnteredCurrentQueue_DoesNotUseOriginalWaitForNextHop()
    {
        // Arrange
        var item = new QueueItem
        {
            ItemId = "i1",
            QueueId = "q2",
            EnqueuedUtc = _now.AddMinutes(-10),
            QueueEnteredUtc = _now.AddSeconds(-10),
            OverflowHistory = ["q1"],
        }.RestorePersistedStatus(QueueItemStatus.Waiting);
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.GetWaitingAsync("q2", It.IsAny<CancellationToken>())).ReturnsAsync([item]);
        var service = CreateService(
            queueItemManager,
            new Mock<IActivityQueueManager>(),
            new Mock<IOmnichannelActivityManager>(),
            new Mock<IBusinessHoursService>());
        var queue = new ActivityQueue { ItemId = "q2", OverflowQueueId = "q3", OverflowAfterSeconds = 30 };

        // Act
        var moved = await service.OverflowDueAsync(queue, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, moved);
        Assert.Equal("q2", item.QueueId);
        queueItemManager.Verify(
            manager => manager.UpdateAsync(It.IsAny<QueueItem>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OverflowDueAsync_WhenDestinationWasPreviouslyVisited_DoesNotCreateCycle()
    {
        // Arrange
        var item = new QueueItem
        {
            ItemId = "i1",
            QueueId = "q1",
            EnqueuedUtc = _now.AddSeconds(-60),
            OverflowHistory = ["q2"],
        }.RestorePersistedStatus(QueueItemStatus.Waiting);
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.GetWaitingAsync("q1", It.IsAny<CancellationToken>())).ReturnsAsync([item]);
        var service = CreateService(
            queueItemManager,
            new Mock<IActivityQueueManager>(),
            new Mock<IOmnichannelActivityManager>(),
            new Mock<IBusinessHoursService>());
        var queue = new ActivityQueue { ItemId = "q1", OverflowQueueId = "q2", OverflowAfterSeconds = 30 };

        // Act
        var moved = await service.OverflowDueAsync(queue, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, moved);
        Assert.Equal("q1", item.QueueId);
        queueItemManager.Verify(
            manager => manager.UpdateAsync(It.IsAny<QueueItem>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OverflowDueAsync_WhenClosedAndAfterHoursOverflow_MovesAllWaitingItems()
    {
        // Arrange
        var item1 = new QueueItem { ItemId = "i1", QueueId = "q1", EnqueuedUtc = _now.AddSeconds(-10) }.RestorePersistedStatus(QueueItemStatus.Waiting);
        var item2 = new QueueItem { ItemId = "i2", QueueId = "q1", EnqueuedUtc = _now.AddSeconds(-5) }.RestorePersistedStatus(QueueItemStatus.Waiting);

        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.GetWaitingAsync("q1", It.IsAny<CancellationToken>())).ReturnsAsync([item1, item2]);

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
        Mock<IBusinessHoursService> businessHours,
        Mock<ISession> session = null,
        Mock<IContactCenterEventPublisher> publisher = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);
        session ??= new Mock<ISession>();
        publisher ??= new Mock<IContactCenterEventPublisher>();
        var scopeExecutor = new Mock<IContactCenterScopeExecutor>();

        TreatmentProvider = new Mock<IQueueTreatmentProvider>();
        InteractionManagerForDequeue = new Mock<IInteractionManager>();
        InteractionManagerForDequeue
            .Setup(x => x.FindByActivityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction { ItemId = "interaction-1", ProviderInteractionId = "ctrl-1" });

        return new ActivityQueueService(
            queueItemManager.Object,
            queueManager.Object,
            activityManager.Object,
            new FakeContactCenterWorkStateService(activityManager.Object),
            businessHours.Object,
            publisher.Object,
            session.Object,
            scopeExecutor.Object,
            TreatmentProvider.Object,
            InteractionManagerForDequeue.Object,
            clock.Object);
    }
}
