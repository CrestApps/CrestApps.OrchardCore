using System.Reflection;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ReservationExpiryBackgroundTaskTests
{
    [Fact]
    public async Task DoWorkAsync_WhenQueueHandlesInboundVoice_UsesVoiceOfferPipeline()
    {
        // Arrange
        var queue = new ActivityQueue
        {
            ItemId = "queue-1",
            EnableSlaAging = true,
            SlaThresholdSeconds = 60,
        };
        var utcNow = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);
        var reservationService = new Mock<IActivityReservationService>();
        var assignmentService = new Mock<IActivityAssignmentService>();
        var queueService = new Mock<IActivityQueueService>();
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager
            .Setup(manager => manager.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([queue]);
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .SetupSequence(manager => manager.FindNextWaitingAsync(It.IsAny<ActivityQueue>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueItem
            {
                ActivityItemId = "activity-1",
                EnqueuedUtc = utcNow.AddMinutes(-5),
            })
            .ReturnsAsync(new QueueItem
            {
                ActivityItemId = "activity-1",
                EnqueuedUtc = utcNow.AddMinutes(-5),
            });
        var interactionManager = new Mock<IInteractionManager>();
        var activityManager = new Mock<IOmnichannelActivityManager>();
        interactionManager
            .Setup(manager => manager.FindByActivityIdAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction
            {
                Channel = InteractionChannel.Voice,
                Direction = InteractionDirection.Inbound,
                ProviderInteractionId = "call-1",
            });

        var inboundVoiceService = new Mock<IInboundVoiceService>();
        inboundVoiceService
            .SetupSequence(service => service.OfferNextAsync("queue-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync("user-1")
            .ReturnsAsync((string)null);
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(utcNow);
        var session = new Mock<ISession>();

        await using var serviceProvider = CreateServiceProvider(
            reservationService,
            assignmentService,
            queueService,
            queueManager,
            queueItemManager,
            interactionManager,
            activityManager,
            inboundVoiceService,
            clock,
            session);

        // Act
        await new ReservationExpiryBackgroundTask().DoWorkAsync(serviceProvider, TestContext.Current.CancellationToken);

        // Assert
        inboundVoiceService.Verify(
            service => service.OfferNextAsync("queue-1", It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        assignmentService.Verify(
            service => service.AssignQueueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        session.Verify(
            value => value.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DoWorkAsync_WhenQueueIsNotInboundVoice_UsesGenericAssignmentPipeline()
    {
        // Arrange
        var queue = new ActivityQueue { ItemId = "queue-1" };
        var reservationService = new Mock<IActivityReservationService>();
        var assignmentService = new Mock<IActivityAssignmentService>();
        var queueService = new Mock<IActivityQueueService>();
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager
            .Setup(manager => manager.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([queue]);
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(manager => manager.FindNextWaitingAsync(It.IsAny<ActivityQueue>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QueueItem)null);
        var interactionManager = new Mock<IInteractionManager>();
        var activityManager = new Mock<IOmnichannelActivityManager>();

        var inboundVoiceService = new Mock<IInboundVoiceService>();
        var clock = new Mock<IClock>();
        var session = new Mock<ISession>();

        await using var serviceProvider = CreateServiceProvider(
            reservationService,
            assignmentService,
            queueService,
            queueManager,
            queueItemManager,
            interactionManager,
            activityManager,
            inboundVoiceService,
            clock,
            session);

        // Act
        await new ReservationExpiryBackgroundTask().DoWorkAsync(serviceProvider, TestContext.Current.CancellationToken);

        // Assert
        assignmentService.Verify(
            service => service.AssignQueueAsync("queue-1", It.IsAny<CancellationToken>()),
            Times.Once);
        inboundVoiceService.Verify(
            service => service.OfferNextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(ActivitySources.PowerDial)]
    [InlineData(ActivitySources.ProgressiveDial)]
    public async Task DoWorkAsync_WhenNextActivityUsesAutomatedDialer_SkipsGenericAssignment(string activitySource)
    {
        // Arrange
        var queue = new ActivityQueue { ItemId = "queue-1" };
        var reservationService = new Mock<IActivityReservationService>();
        var assignmentService = new Mock<IActivityAssignmentService>();
        var queueService = new Mock<IActivityQueueService>();
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager
            .Setup(manager => manager.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([queue]);
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(manager => manager.FindNextWaitingAsync(It.IsAny<ActivityQueue>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueItem { ActivityItemId = "activity-1" });
        var interactionManager = new Mock<IInteractionManager>();
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager
            .Setup(manager => manager.FindByIdAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelActivity
            {
                ItemId = "activity-1",
                Source = activitySource,
            });
        var inboundVoiceService = new Mock<IInboundVoiceService>();
        var clock = new Mock<IClock>();
        var session = new Mock<ISession>();

        await using var serviceProvider = CreateServiceProvider(
            reservationService,
            assignmentService,
            queueService,
            queueManager,
            queueItemManager,
            interactionManager,
            activityManager,
            inboundVoiceService,
            clock,
            session);

        // Act
        await new ReservationExpiryBackgroundTask().DoWorkAsync(serviceProvider, TestContext.Current.CancellationToken);

        // Assert
        assignmentService.Verify(
            service => service.AssignQueueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        inboundVoiceService.Verify(
            service => service.OfferNextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DoWorkAsync_WhenVoiceOfferLimitIsReached_DoesNotUseGenericAssignmentPipeline()
    {
        // Arrange
        var queue = new ActivityQueue { ItemId = "queue-1" };
        var reservationService = new Mock<IActivityReservationService>();
        var assignmentService = new Mock<IActivityAssignmentService>();
        var queueService = new Mock<IActivityQueueService>();
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager
            .Setup(manager => manager.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([queue]);
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(manager => manager.FindNextWaitingAsync(It.IsAny<ActivityQueue>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueItem { ActivityItemId = "activity-1" });
        var interactionManager = new Mock<IInteractionManager>();
        var activityManager = new Mock<IOmnichannelActivityManager>();
        interactionManager
            .Setup(manager => manager.FindByActivityIdAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction
            {
                Channel = InteractionChannel.Voice,
                Direction = InteractionDirection.Inbound,
                ProviderInteractionId = "call-1",
            });
        var inboundVoiceService = new Mock<IInboundVoiceService>();
        inboundVoiceService
            .Setup(service => service.OfferNextAsync("queue-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync("user-1");
        var clock = new Mock<IClock>();
        var session = new Mock<ISession>();

        await using var serviceProvider = CreateServiceProvider(
            reservationService,
            assignmentService,
            queueService,
            queueManager,
            queueItemManager,
            interactionManager,
            activityManager,
            inboundVoiceService,
            clock,
            session);

        // Act
        await new ReservationExpiryBackgroundTask().DoWorkAsync(serviceProvider, TestContext.Current.CancellationToken);

        // Assert
        inboundVoiceService.Verify(
            service => service.OfferNextAsync("queue-1", It.IsAny<CancellationToken>()),
            Times.Exactly(100));
        session.Verify(
            value => value.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(100));
        assignmentService.Verify(
            service => service.AssignQueueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DoWorkAsync_WhenRoutingFeatureIsQuiescing_DoesNoWork()
    {
        // Arrange
        // A node that is draining the Work Distribution feature must not admit new reservation/assignment work. The task
        // resolves the work manager first and returns immediately when the lease is denied, so no queue, voice, or
        // assignment work runs on a quiescing node.
        var workManager = new TestContactCenterFeatureWorkManager();
        workManager.Quiesce(ContactCenterConstants.Feature.Queues);

        var reservationService = new Mock<IActivityReservationService>(MockBehavior.Strict);
        var assignmentService = new Mock<IActivityAssignmentService>(MockBehavior.Strict);
        var queueService = new Mock<IActivityQueueService>(MockBehavior.Strict);
        var queueManager = new Mock<IActivityQueueManager>(MockBehavior.Strict);
        var queueItemManager = new Mock<IQueueItemManager>(MockBehavior.Strict);
        var interactionManager = new Mock<IInteractionManager>(MockBehavior.Strict);
        var activityManager = new Mock<IOmnichannelActivityManager>(MockBehavior.Strict);
        var inboundVoiceService = new Mock<IInboundVoiceService>(MockBehavior.Strict);
        var clock = new Mock<IClock>(MockBehavior.Strict);
        var session = new Mock<ISession>(MockBehavior.Strict);

        await using var serviceProvider = CreateServiceProvider(
            reservationService,
            assignmentService,
            queueService,
            queueManager,
            queueItemManager,
            interactionManager,
            activityManager,
            inboundVoiceService,
            clock,
            session,
            workManager);

        // Act
        await new ReservationExpiryBackgroundTask().DoWorkAsync(serviceProvider, TestContext.Current.CancellationToken);

        // Assert
        reservationService.VerifyNoOtherCalls();
        assignmentService.VerifyNoOtherCalls();
        queueManager.VerifyNoOtherCalls();
        Assert.Equal(0, workManager.ActiveLeaseCount);
    }

    [Fact]
    public void BackgroundTaskMetadata_RunBudget_StaysBelowLockExpiration_WhichHasSafeMarginAboveSchedule()
    {
        // Arrange
        // Self-overlap is prevented by bounding each run to a wall-clock budget that is strictly below the
        // distributed-lock expiration, and by keeping that expiration above the schedule interval. Read both
        // private budget/lock constants and assert the ordering: schedule < 2x <= lock-expiration, and
        // run-budget < lock-expiration, and lock-expiration > acquire-timeout.
        var attribute = typeof(ReservationExpiryBackgroundTask)
            .GetCustomAttributes(typeof(BackgroundTaskAttribute), inherit: false)
            .Cast<BackgroundTaskAttribute>()
            .Single();
        var lockExpirationMilliseconds = ReadPrivateConstant("LockExpirationMilliseconds");
        var maxRunDurationMilliseconds = ReadPrivateConstant("MaxRunDurationMilliseconds");

        // Act
        const int scheduleIntervalMilliseconds = 60_000;

        // Assert
        Assert.Equal("* * * * *", attribute.Schedule);
        Assert.Equal(lockExpirationMilliseconds, attribute.LockExpiration);
        Assert.True(
            attribute.LockExpiration >= 2 * scheduleIntervalMilliseconds,
            $"LockExpiration ({attribute.LockExpiration} ms) must be at least twice the {scheduleIntervalMilliseconds} ms schedule interval.");
        Assert.True(
            attribute.LockExpiration > attribute.LockTimeout,
            $"LockExpiration ({attribute.LockExpiration} ms) must exceed LockTimeout ({attribute.LockTimeout} ms).");
        Assert.True(
            maxRunDurationMilliseconds < lockExpirationMilliseconds,
            $"MaxRunDuration ({maxRunDurationMilliseconds} ms) must stay below LockExpiration ({lockExpirationMilliseconds} ms) so a run cannot outlive its lock.");
    }

    [Fact]
    public async Task DoWorkAsync_WhenRunExceedsItsTimeBudget_DefersRemainingQueuesToTheNextTick()
    {
        // Arrange
        // The run is bounded to a wall-clock budget below the lock expiration. Advance the clock past the
        // budget while processing the first queue and assert the second queue is not touched, proving a run
        // cannot grow without bound (and therefore cannot outlive its lock and self-overlap).
        var current = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(() => current);

        var queue1 = new ActivityQueue { ItemId = "queue-1" };
        var queue2 = new ActivityQueue { ItemId = "queue-2" };
        var reservationService = new Mock<IActivityReservationService>();
        var assignmentService = new Mock<IActivityAssignmentService>();
        var queueService = new Mock<IActivityQueueService>();
        queueService
            .Setup(service => service.OverflowDueAsync(queue1, It.IsAny<CancellationToken>()))
            .Callback(() => current = current.AddMilliseconds(100_000))
            .ReturnsAsync(0);
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager
            .Setup(manager => manager.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([queue1, queue2]);
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(manager => manager.FindNextWaitingAsync(It.IsAny<ActivityQueue>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QueueItem)null);
        var interactionManager = new Mock<IInteractionManager>();
        var activityManager = new Mock<IOmnichannelActivityManager>();
        var inboundVoiceService = new Mock<IInboundVoiceService>();
        var session = new Mock<ISession>();

        await using var serviceProvider = CreateServiceProvider(
            reservationService,
            assignmentService,
            queueService,
            queueManager,
            queueItemManager,
            interactionManager,
            activityManager,
            inboundVoiceService,
            clock,
            session);

        // Act
        await new ReservationExpiryBackgroundTask().DoWorkAsync(serviceProvider, TestContext.Current.CancellationToken);

        // Assert
        queueService.Verify(
            service => service.OverflowDueAsync(queue1, It.IsAny<CancellationToken>()),
            Times.Once);
        queueService.Verify(
            service => service.OverflowDueAsync(queue2, It.IsAny<CancellationToken>()),
            Times.Never);
        assignmentService.Verify(
            service => service.AssignQueueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DoWorkAsync_WhenShutdownCancels_PropagatesCancellation()
    {
        // Arrange
        // On shutdown Orchard cancels the running task. The per-queue error handler must rethrow cancellation
        // instead of swallowing it, so the task stops promptly and its work lease is released.
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var queue = new ActivityQueue { ItemId = "queue-1" };
        var reservationService = new Mock<IActivityReservationService>();
        var assignmentService = new Mock<IActivityAssignmentService>();
        var queueService = new Mock<IActivityQueueService>();
        queueService
            .Setup(service => service.OverflowDueAsync(It.IsAny<ActivityQueue>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cancellationSource.Token));
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager
            .Setup(manager => manager.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([queue]);
        var queueItemManager = new Mock<IQueueItemManager>();
        var interactionManager = new Mock<IInteractionManager>();
        var activityManager = new Mock<IOmnichannelActivityManager>();
        var inboundVoiceService = new Mock<IInboundVoiceService>();
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc));
        var session = new Mock<ISession>();

        await using var serviceProvider = CreateServiceProvider(
            reservationService,
            assignmentService,
            queueService,
            queueManager,
            queueItemManager,
            interactionManager,
            activityManager,
            inboundVoiceService,
            clock,
            session);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new ReservationExpiryBackgroundTask().DoWorkAsync(serviceProvider, cancellationSource.Token));
    }

    private static int ReadPrivateConstant(string name)
    {
        var field = typeof(ReservationExpiryBackgroundTask)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Static);

        return (int)field.GetRawConstantValue();
    }

    private static ServiceProvider CreateServiceProvider(
        Mock<IActivityReservationService> reservationService,
        Mock<IActivityAssignmentService> assignmentService,
        Mock<IActivityQueueService> queueService,
        Mock<IActivityQueueManager> queueManager,
        Mock<IQueueItemManager> queueItemManager,
        Mock<IInteractionManager> interactionManager,
        Mock<IOmnichannelActivityManager> activityManager,
        Mock<IInboundVoiceService> inboundVoiceService,
        Mock<IClock> clock,
        Mock<ISession> session,
        IContactCenterFeatureWorkManager workManager = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(reservationService.Object);
        services.AddSingleton(assignmentService.Object);
        services.AddSingleton(queueService.Object);
        services.AddSingleton(queueManager.Object);
        services.AddSingleton(queueItemManager.Object);
        services.AddSingleton(interactionManager.Object);
        services.AddSingleton(activityManager.Object);
        services.AddSingleton(inboundVoiceService.Object);
        services.AddSingleton(clock.Object);
        services.AddSingleton(session.Object);
        services.AddSingleton<IContactCenterFeatureWorkManager>(workManager ?? new TestContactCenterFeatureWorkManager());
        services.AddLogging();

        return services.BuildServiceProvider();
    }
}
