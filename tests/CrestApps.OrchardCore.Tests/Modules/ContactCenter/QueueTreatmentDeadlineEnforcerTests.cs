using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Each queue holds one in-process deadline for the next thing any of its callers is due, so announcements keep their
/// timing without the queue-treatment sweep holding the background loop to tick for them.
/// </summary>
public sealed class QueueTreatmentDeadlineEnforcerTests
{
    private static readonly DateTime _now = new(2026, 9, 24, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ACallerWhoHasJustArrived_IsDueTheirWelcomeAtOnce()
    {
        // Arrange
        var settings = new QueueTreatmentSettings { WelcomeMessage = "Thanks for calling.", AnnouncementIntervalSeconds = 30, AnnouncePosition = true };
        var item = CreateItem(enteredUtc: _now);

        // Act
        var dueUtc = QueueTreatmentPolicy.GetNextDueUtc(item, settings);

        // Assert
        Assert.Equal(_now, dueUtc);
    }

    [Fact]
    public void ACallerWhoHeardTheirLastUpdate_IsDueTheNextOneAnIntervalLater()
    {
        // Arrange
        var settings = new QueueTreatmentSettings { WelcomeMessage = "Thanks for calling.", AnnouncementIntervalSeconds = 30, AnnouncePosition = true };
        var item = CreateItem(enteredUtc: _now.AddSeconds(-40));
        item.TreatmentStepsPlayed = 2;
        item.LastTreatmentUtc = _now.AddSeconds(-5);

        // Act
        var dueUtc = QueueTreatmentPolicy.GetNextDueUtc(item, settings);

        // Assert
        Assert.Equal(_now.AddSeconds(25), dueUtc);
    }

    [Fact]
    public void ACallbackOfferDueBeforeTheNextUpdate_IsWhatComesDueFirst()
    {
        // Arrange
        var settings = new QueueTreatmentSettings
        {
            HoldMusicMediaId = "music",
            AnnouncementIntervalSeconds = 60,
            AnnouncePosition = true,
            CallbackDtmfKey = "1",
            CallbackOfferAfterSeconds = 20,
        };
        var item = CreateItem(enteredUtc: _now);
        item.TreatmentStepsPlayed = 1;
        item.LastTreatmentUtc = _now;

        // Act
        var dueUtc = QueueTreatmentPolicy.GetNextDueUtc(item, settings);

        // Assert
        Assert.Equal(_now.AddSeconds(20), dueUtc);
    }

    [Fact]
    public void ACadenceWithNothingToSay_IsNothingToWaitFor()
    {
        // Arrange
        var settings = new QueueTreatmentSettings { HoldMusicMediaId = "music", AnnouncementIntervalSeconds = 30 };
        var item = CreateItem(enteredUtc: _now);
        item.TreatmentStepsPlayed = 1;
        item.LastTreatmentUtc = _now;

        // Act
        var dueUtc = QueueTreatmentPolicy.GetNextDueUtc(item, settings);

        // Assert
        Assert.Null(dueUtc);
    }

    [Fact]
    public async Task ArmAsync_HoldsTheQueuesSoonestDeadline()
    {
        // Arrange
        var soon = CreateItem(enteredUtc: _now.AddSeconds(-50));
        soon.TreatmentStepsPlayed = 1;
        soon.LastTreatmentUtc = _now.AddSeconds(-20);
        var later = CreateItem(enteredUtc: _now.AddSeconds(-10), itemId: "item-2");
        later.TreatmentStepsPlayed = 1;
        later.LastTreatmentUtc = _now.AddSeconds(-10);
        var (enforcer, scheduler, _, _) = CreateEnforcer([soon, later]);

        // Act
        await enforcer.ArmAsync("queue-1", TestContext.Current.CancellationToken);

        // Assert
        scheduler.Verify(
            value => value.Schedule(
                QueueTreatmentDeadlineEnforcer.GetDeadlineKey("queue-1"),
                _now.AddSeconds(10),
                It.IsAny<Func<IServiceProvider, CancellationToken, Task<DateTime?>>>()),
            Times.Once);
    }

    [Fact]
    public async Task ArmAsync_DropsTheDeadline_WhenNobodyIsWaiting()
    {
        // Arrange
        var (enforcer, scheduler, _, _) = CreateEnforcer([]);

        // Act
        await enforcer.ArmAsync("queue-1", TestContext.Current.CancellationToken);

        // Assert
        scheduler.Verify(value => value.Cancel(QueueTreatmentDeadlineEnforcer.GetDeadlineKey("queue-1")), Times.Once);
        scheduler.Verify(
            value => value.Schedule(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<Func<IServiceProvider, CancellationToken, Task<DateTime?>>>()),
            Times.Never);
    }

    [Fact]
    public async Task RunDueAsync_PlaysWhatIsDue_CommitsIt_AndReturnsTheNextDeadline()
    {
        // Arrange
        var item = CreateItem(enteredUtc: _now.AddSeconds(-30));
        var (enforcer, _, treatment, session) = CreateEnforcer([item]);
        treatment
            .Setup(value => value.RunDueAsync(It.IsAny<ActivityQueue>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                // The pass plays the next update to the caller.
                item.TreatmentStepsPlayed = 2;
                item.LastTreatmentUtc = _now;
            })
            .ReturnsAsync(1);

        // Act
        var next = await enforcer.RunDueAsync("queue-1", TestContext.Current.CancellationToken);

        // Assert
        treatment.Verify(value => value.RunDueAsync(It.Is<ActivityQueue>(queue => queue.ItemId == "queue-1"), It.IsAny<CancellationToken>()), Times.Once);
        session.Verify(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(_now.AddSeconds(30), next);
    }

    [Fact]
    public async Task RunDueAsync_LooksAgainLater_AtACallerItCouldNotTreat()
    {
        // Arrange
        // Due an update, but the pass could not play it (the call has no live leg): the deadline must not be "now"
        // again, or the timer would spin.
        var item = CreateItem(enteredUtc: _now.AddSeconds(-90));
        item.TreatmentStepsPlayed = 1;
        item.LastTreatmentUtc = _now.AddSeconds(-60);
        var (enforcer, _, _, _) = CreateEnforcer([item]);

        // Act
        var next = await enforcer.RunDueAsync("queue-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_now + QueueTreatmentDeadlineEnforcer.RetryInterval, next);
    }

    [Fact]
    public async Task RunDueAsync_PlaysNothing_WhileAnotherPassHoldsTheQueue()
    {
        // Arrange
        var item = CreateItem(enteredUtc: _now);
        var (enforcer, _, treatment, _) = CreateEnforcer([item], lockAcquired: false);

        // Act
        var next = await enforcer.RunDueAsync("queue-1", TestContext.Current.CancellationToken);

        // Assert
        treatment.Verify(value => value.RunDueAsync(It.IsAny<ActivityQueue>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(_now + QueueTreatmentDeadlineEnforcer.LockedRetryInterval, next);
    }

    [Fact]
    public async Task RunDueAsync_TakesNoLock_ForAQueueWhoseTreatmentPlaysNothing()
    {
        // Arrange
        // The minute sweep asks this of every queue, and most have nothing configured. The lock is held elsewhere, so a
        // pass that asked for it first would come back with a retry instead of with nothing to do.
        var item = CreateItem(enteredUtc: _now);
        var (enforcer, _, treatment, _) = CreateEnforcer([item], lockAcquired: false, settings: new QueueTreatmentSettings());

        // Act
        var next = await enforcer.RunDueAsync("queue-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(next);
        treatment.Verify(value => value.RunDueAsync(It.IsAny<ActivityQueue>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAndArmAsync_HoldsTheDeadlineThePassReturns()
    {
        // Arrange
        var item = CreateItem(enteredUtc: _now.AddSeconds(-20));
        item.TreatmentStepsPlayed = 1;
        item.LastTreatmentUtc = _now.AddSeconds(-20);
        var (enforcer, scheduler, _, _) = CreateEnforcer([item]);

        // Act
        await enforcer.RunAndArmAsync("queue-1", TestContext.Current.CancellationToken);

        // Assert
        scheduler.Verify(
            value => value.Schedule(
                QueueTreatmentDeadlineEnforcer.GetDeadlineKey("queue-1"),
                _now.AddSeconds(10),
                It.IsAny<Func<IServiceProvider, CancellationToken, Task<DateTime?>>>()),
            Times.Once);
    }

    [Fact]
    public async Task ACallerEnteringTheQueue_ArmsItsTreatmentDeadline()
    {
        // Arrange
        var item = CreateItem(enteredUtc: _now);
        var queueItems = new Mock<IQueueItemManager>();
        queueItems.Setup(manager => manager.FindByIdAsync("item-1", It.IsAny<CancellationToken>())).ReturnsAsync(item);
        var enforcer = new Mock<IQueueTreatmentDeadlineEnforcer>();
        var handler = new QueueTreatmentDeadlineEventHandler(
            new Lazy<IQueueTreatmentDeadlineEnforcer>(() => enforcer.Object),
            queueItems.Object,
            new Mock<IActivityReservationManager>().Object);

        // Act
        await handler.HandleAsync(new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.QueueItemAdded,
            AggregateType = nameof(QueueItem),
            AggregateId = "item-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        enforcer.Verify(value => value.ArmAsync("queue-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static QueueItem CreateItem(DateTime enteredUtc, string itemId = "item-1")
    {
        var item = new QueueItem
        {
            ItemId = itemId,
            QueueId = "queue-1",
            ActivityItemId = "activity-" + itemId,
            EnqueuedUtc = enteredUtc,
            QueueEnteredUtc = enteredUtc,
        };

        item.TransitionTo(QueueItemStatus.Waiting);

        return item;
    }

    private static (QueueTreatmentDeadlineEnforcer Enforcer, Mock<IContactCenterDeadlineScheduler> Scheduler, Mock<IQueueTreatmentService> Treatment, Mock<ISession> Session) CreateEnforcer(
        IReadOnlyCollection<QueueItem> waiting,
        bool lockAcquired = true,
        QueueTreatmentSettings settings = null)
    {
        var queue = new ActivityQueue
        {
            ItemId = "queue-1",
            Enabled = true,
            Treatment = settings ?? new QueueTreatmentSettings
            {
                WelcomeMessage = "Thanks for calling.",
                AnnouncementIntervalSeconds = 30,
                AnnouncePosition = true,
            },
        };

        var queueManager = new Mock<IActivityQueueManager>();
        queueManager.Setup(manager => manager.FindByIdAsync("queue-1", It.IsAny<CancellationToken>())).ReturnsAsync(queue);

        var queueItems = new Mock<IQueueItemManager>();
        queueItems.Setup(manager => manager.GetWaitingAsync("queue-1", It.IsAny<CancellationToken>())).ReturnsAsync(waiting);

        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(() => lockAcquired ? (new Mock<ILocker>().Object, true) : (null, false));

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        var scheduler = new Mock<IContactCenterDeadlineScheduler>();
        var treatment = new Mock<IQueueTreatmentService>();
        var session = new Mock<ISession>();

        var enforcer = new QueueTreatmentDeadlineEnforcer(
            scheduler.Object,
            queueManager.Object,
            queueItems.Object,
            treatment.Object,
            distributedLock.Object,
            session.Object,
            clock.Object);

        return (enforcer, scheduler, treatment, session);
    }
}
