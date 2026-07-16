using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
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
            .Setup(manager => manager.ListEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([queue]);
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .SetupSequence(manager => manager.ListWaitingAsync("queue-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new QueueItem
                {
                    ActivityItemId = "manual-activity",
                    EnqueuedUtc = utcNow,
                },
                new QueueItem
                {
                    ActivityItemId = "activity-1",
                    EnqueuedUtc = utcNow.AddMinutes(-5),
                },
            ])
            .ReturnsAsync(
            [
                new QueueItem
                {
                    ActivityItemId = "activity-1",
                    EnqueuedUtc = utcNow.AddMinutes(-5),
                },
            ]);
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
            .Setup(manager => manager.ListEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([queue]);
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(manager => manager.ListWaitingAsync("queue-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<QueueItem>());
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
            .Setup(manager => manager.ListEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([queue]);
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(manager => manager.ListWaitingAsync("queue-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new QueueItem { ActivityItemId = "activity-1" }]);
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
            .Setup(manager => manager.ListEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([queue]);
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(manager => manager.ListWaitingAsync("queue-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new QueueItem { ActivityItemId = "activity-1" }]);
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
        Mock<ISession> session)
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
        services.AddLogging();

        return services.BuildServiceProvider();
    }
}
