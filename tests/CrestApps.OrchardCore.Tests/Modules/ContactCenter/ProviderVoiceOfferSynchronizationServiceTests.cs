using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ProviderVoiceOfferSynchronizationServiceTests
{
    [Fact]
    public async Task ReconcileEndedOfferAsync_WhenPreConnectOfferEnded_RemovesQueueAndReleasesAgent()
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "int1",
            ActivityItemId = "act1",
            AgentId = "agent-1",
            AnsweredUtc = new DateTime(2026, 7, 10, 11, 59, 0, DateTimeKind.Utc),
        }.RestorePersistedStatus(InteractionStatus.Ended);
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "int1",
            ActivityItemId = "act1",
            AgentId = "agent-1",
            AnsweredUtc = interaction.AnsweredUtc,
        }.RestorePersistedState(VoiceCallState.Ended);
        var queueItem = new QueueItem
        {
            ItemId = "queue-1",
            ActivityItemId = "act1",
            ReservationId = "res-1",
        }.RestorePersistedStatus(QueueItemStatus.Reserved);
        var reservation = new ActivityReservation
        {
            ItemId = "res-1",
            AgentId = "agent-1",
            ActivityItemId = "act1",
        }.RestorePersistedStatus(ReservationStatus.Pending);
        var agent = new AgentProfile
        {
            ItemId = "agent-1",
            ActiveReservationId = "res-1",
            PresenceStatus = AgentPresenceStatus.WrapUp,
            QueueIds = ["queue-1"],
        };
        var activity = new OmnichannelActivity
        {
            ItemId = "act1",
            AssignmentStatus = ActivityAssignmentStatus.Reserved,
            AssignedToId = "user-1",
            ReservationId = "res-1",
        };

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);

        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager.Setup(m => m.FindByInteractionIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(session);

        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.FindByActivityIdAsync("act1", It.IsAny<CancellationToken>())).ReturnsAsync(queueItem);

        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.GetActiveByActivityAsync("act1", It.IsAny<CancellationToken>())).ReturnsAsync([reservation]);

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("agent-1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);

        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.FindByIdAsync("act1", It.IsAny<CancellationToken>())).ReturnsAsync(activity);

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc));

        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<ProviderVoiceOfferSynchronizationService>>();
        var service = new ProviderVoiceOfferSynchronizationService(
            interactionManager.Object,
            callSessionManager.Object,
            queueItemManager.Object,
            reservationManager.Object,
            agentManager.Object,
            activityManager.Object,
            new FakeContactCenterWorkStateService(activityManager.Object),
            CreateServiceProvider(),
            clock.Object,
            logger.Object);

        // Act
        await service.ReconcileEndedOfferAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        queueItemManager.Verify(
            m => m.UpdateAsync(
                It.Is<QueueItem>(value => value.Status == QueueItemStatus.Removed && value.DequeuedUtc.HasValue),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
        reservationManager.Verify(
            m => m.UpdateAsync(
                It.Is<ActivityReservation>(value => value.Status == ReservationStatus.Canceled),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
        agentManager.Verify(
            m => m.UpdateAsync(
                It.Is<AgentProfile>(value => value.ActiveReservationId == null && value.PresenceStatus == AgentPresenceStatus.Available),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
        activityManager.Verify(
            m => m.UpdateAsync(
                It.Is<OmnichannelActivity>(value =>
                    value.AssignmentStatus == ActivityAssignmentStatus.Released &&
                    value.AssignedToId == null &&
                    value.ReservationId == null),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReconcileEndedOfferAsync_WhenTerminalInteractionIsStillWaiting_RemovesQueueItem()
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "int1",
            ActivityItemId = "act1",
        }.RestorePersistedStatus(InteractionStatus.Ended);
        var queueItem = new QueueItem
        {
            ItemId = "queue-1",
            ActivityItemId = "act1",
        }.RestorePersistedStatus(QueueItemStatus.Waiting);
        var activity = new OmnichannelActivity
        {
            ItemId = "act1",
            AssignmentStatus = ActivityAssignmentStatus.Available,
        };

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(m => m.FindByInteractionIdAsync("int1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallSession
            {
                InteractionId = "int1",
            }.RestorePersistedState(VoiceCallState.Ended));
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.FindByActivityIdAsync("act1", It.IsAny<CancellationToken>())).ReturnsAsync(queueItem);
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.GetActiveByActivityAsync("act1", It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.FindByIdAsync("act1", It.IsAny<CancellationToken>())).ReturnsAsync(activity);
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc));
        var service = new ProviderVoiceOfferSynchronizationService(
            interactionManager.Object,
            callSessionManager.Object,
            queueItemManager.Object,
            reservationManager.Object,
            new Mock<IAgentProfileManager>().Object,
            activityManager.Object,
            new FakeContactCenterWorkStateService(activityManager.Object),
            CreateServiceProvider(),
            clock.Object,
            new Mock<Microsoft.Extensions.Logging.ILogger<ProviderVoiceOfferSynchronizationService>>().Object);

        // Act
        await service.ReconcileEndedOfferAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        queueItemManager.Verify(
            m => m.UpdateAsync(
                It.Is<QueueItem>(value => value.Status == QueueItemStatus.Removed && value.DequeuedUtc.HasValue),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
        activityManager.Verify(
            m => m.UpdateAsync(
                It.Is<OmnichannelActivity>(value => value.AssignmentStatus == ActivityAssignmentStatus.Released),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReconcileEndedOfferAsync_WhenAnsweredCallEnded_CompletesAssignedQueueItem()
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "int1",
            ActivityItemId = "act1",
            AgentId = "agent-1",
            AnsweredUtc = new DateTime(2026, 7, 10, 11, 59, 0, DateTimeKind.Utc),
        }.RestorePersistedStatus(InteractionStatus.Ended);
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "int1",
            ActivityItemId = "act1",
            AgentId = "agent-1",
            AnsweredUtc = interaction.AnsweredUtc,
        }.RestorePersistedState(VoiceCallState.Ended);
        var queueItem = new QueueItem
        {
            ItemId = "queue-1",
            ActivityItemId = "act1",
            ReservationId = "res-1",
        }.RestorePersistedStatus(QueueItemStatus.Assigned);

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);

        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager.Setup(m => m.FindByInteractionIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(session);

        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.FindByActivityIdAsync("act1", It.IsAny<CancellationToken>())).ReturnsAsync(queueItem);

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc));

        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.GetActiveByActivityAsync("act1", It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var agentManager = new Mock<IAgentProfileManager>();
        var presenceManager = new Mock<IAgentPresenceManager>();
        var activityManager = new Mock<IOmnichannelActivityManager>();
        var service = new ProviderVoiceOfferSynchronizationService(
            interactionManager.Object,
            callSessionManager.Object,
            queueItemManager.Object,
            reservationManager.Object,
            agentManager.Object,
            activityManager.Object,
            new FakeContactCenterWorkStateService(activityManager.Object),
            CreateServiceProvider(presenceManager.Object),
            clock.Object,
            new Mock<Microsoft.Extensions.Logging.ILogger<ProviderVoiceOfferSynchronizationService>>().Object);

        // Act
        await service.ReconcileEndedOfferAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        queueItemManager.Verify(
            m => m.UpdateAsync(
                It.Is<QueueItem>(value => value.Status == QueueItemStatus.Completed && value.DequeuedUtc.HasValue),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
        reservationManager.Verify(
            m => m.UpdateAsync(It.IsAny<ActivityReservation>(), null, It.IsAny<CancellationToken>()),
            Times.Never);
        agentManager.Verify(
            m => m.UpdateAsync(It.IsAny<AgentProfile>(), null, It.IsAny<CancellationToken>()),
            Times.Never);
        activityManager.Verify(
            m => m.UpdateAsync(It.IsAny<OmnichannelActivity>(), null, It.IsAny<CancellationToken>()),
            Times.Never);
        presenceManager.Verify(
            m => m.StartWrapUpAsync("agent-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReconcileEndedOfferAsync_WhenActivityWasAlreadyCompleted_DoesNotRestoreWrapUp()
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "int1",
            ActivityItemId = "act1",
            AgentId = "agent-1",
            AnsweredUtc = new DateTime(2026, 7, 10, 11, 59, 0, DateTimeKind.Utc),
        }.RestorePersistedStatus(InteractionStatus.Ended);
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "int1",
            ActivityItemId = "act1",
            AgentId = "agent-1",
            AnsweredUtc = interaction.AnsweredUtc,
        }.RestorePersistedState(VoiceCallState.Ended);
        var queueItem = new QueueItem
        {
            ItemId = "queue-1",
            ActivityItemId = "act1",
        }.RestorePersistedStatus(QueueItemStatus.Assigned);
        var agent = new AgentProfile
        {
            ItemId = "agent-1",
            PresenceStatus = AgentPresenceStatus.Busy,
        };

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager.Setup(m => m.FindByInteractionIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(session);
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.FindByActivityIdAsync("act1", It.IsAny<CancellationToken>())).ReturnsAsync(queueItem);
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.GetActiveByActivityAsync("act1", It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("agent-1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager
            .Setup(m => m.FindByIdAsync("act1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelActivity
            {
                ItemId = "act1",
                Status = ActivityStatus.Completed,
            });
        var presenceManager = new Mock<IAgentPresenceManager>();
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc));
        var service = new ProviderVoiceOfferSynchronizationService(
            interactionManager.Object,
            callSessionManager.Object,
            queueItemManager.Object,
            reservationManager.Object,
            agentManager.Object,
            activityManager.Object,
            new FakeContactCenterWorkStateService(activityManager.Object),
            CreateServiceProvider(presenceManager.Object),
            clock.Object,
            new Mock<Microsoft.Extensions.Logging.ILogger<ProviderVoiceOfferSynchronizationService>>().Object);

        // Act
        await service.ReconcileEndedOfferAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        presenceManager.Verify(
            manager => manager.CompleteWorkAsync("agent-1", It.IsAny<CancellationToken>()),
            Times.Once);
        presenceManager.Verify(
            manager => manager.StartWrapUpAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReconcileEndedOfferAsync_WhenAnsweredCallTransferred_CompletesAssignedQueueItem()
    {
        // Arrange
        var answeredUtc = new DateTime(2026, 7, 10, 11, 59, 0, DateTimeKind.Utc);
        var interaction = new Interaction
        {
            ItemId = "int1",
            ActivityItemId = "act1",
            AgentId = "agent-1",
            AnsweredUtc = answeredUtc,
        }.RestorePersistedStatus(InteractionStatus.Transferring);
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "int1",
            ActivityItemId = "act1",
            AgentId = "agent-1",
            AnsweredUtc = answeredUtc,
        }.RestorePersistedState(VoiceCallState.Transferred);
        var queueItem = new QueueItem
        {
            ItemId = "queue-1",
            ActivityItemId = "act1",
            ReservationId = "res-1",
        }.RestorePersistedStatus(QueueItemStatus.Assigned);

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);

        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager.Setup(m => m.FindByInteractionIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(session);

        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.FindByActivityIdAsync("act1", It.IsAny<CancellationToken>())).ReturnsAsync(queueItem);

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc));

        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.GetActiveByActivityAsync("act1", It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var service = new ProviderVoiceOfferSynchronizationService(
            interactionManager.Object,
            callSessionManager.Object,
            queueItemManager.Object,
            reservationManager.Object,
            new Mock<IAgentProfileManager>().Object,
            new Mock<IOmnichannelActivityManager>().Object,
            new FakeContactCenterWorkStateService(),
            CreateServiceProvider(),
            clock.Object,
            new Mock<Microsoft.Extensions.Logging.ILogger<ProviderVoiceOfferSynchronizationService>>().Object);

        // Act
        await service.ReconcileEndedOfferAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        queueItemManager.Verify(
            m => m.UpdateAsync(
                It.Is<QueueItem>(value => value.Status == QueueItemStatus.Completed && value.DequeuedUtc.HasValue),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReconcileEndedOfferAsync_WhenMultipleReservationsExist_CancelsAllOfThemAndReleasesAgent()
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "int1",
            ActivityItemId = "act1",
            AgentId = "agent-1",
        }.RestorePersistedStatus(InteractionStatus.Ended);
        var queueItem = new QueueItem
        {
            ItemId = "queue-1",
            ActivityItemId = "act1",
            ReservationId = "res-2",
        }.RestorePersistedStatus(QueueItemStatus.Assigned);
        var reservations = new List<ActivityReservation>
        {
            new ActivityReservation { ItemId = "res-1", AgentId = "agent-1", ActivityItemId = "act1" }.RestorePersistedStatus(ReservationStatus.Pending),
            new ActivityReservation { ItemId = "res-2", AgentId = "agent-1", ActivityItemId = "act1" }.RestorePersistedStatus(ReservationStatus.Accepted),
            new ActivityReservation { ItemId = "res-3", AgentId = "agent-1", ActivityItemId = "act1" }.RestorePersistedStatus(ReservationStatus.Accepted),
        };
        var agent = new AgentProfile
        {
            ItemId = "agent-1",
            ActiveReservationId = "res-2",
            PresenceStatus = AgentPresenceStatus.Busy,
            QueueIds = ["queue-1"],
        };
        var activity = new OmnichannelActivity
        {
            ItemId = "act1",
            AssignmentStatus = ActivityAssignmentStatus.Assigned,
            AssignedToId = "user-1",
            ReservationId = "res-2",
        };

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);

        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager.Setup(m => m.FindByInteractionIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync((CallSession)null);

        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.FindByActivityIdAsync("act1", It.IsAny<CancellationToken>())).ReturnsAsync(queueItem);

        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.GetActiveByActivityAsync("act1", It.IsAny<CancellationToken>())).ReturnsAsync(reservations);

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("agent-1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);

        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.FindByIdAsync("act1", It.IsAny<CancellationToken>())).ReturnsAsync(activity);

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc));

        var service = new ProviderVoiceOfferSynchronizationService(
            interactionManager.Object,
            callSessionManager.Object,
            queueItemManager.Object,
            reservationManager.Object,
            agentManager.Object,
            activityManager.Object,
            new FakeContactCenterWorkStateService(activityManager.Object),
            CreateServiceProvider(),
            clock.Object,
            new Mock<Microsoft.Extensions.Logging.ILogger<ProviderVoiceOfferSynchronizationService>>().Object);

        // Act
        await service.ReconcileEndedOfferAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        reservationManager.Verify(
            m => m.UpdateAsync(
                It.Is<ActivityReservation>(value => value.Status == ReservationStatus.Canceled),
                null,
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));
        agentManager.Verify(
            m => m.UpdateAsync(
                It.Is<AgentProfile>(value => value.ActiveReservationId == null && value.PresenceStatus == AgentPresenceStatus.Available),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReconcileEndedOfferAsync_WhenAnsweredCallHasLingeringReservation_CancelsItAndClearsAgentPointer()
    {
        // Arrange
        var answeredUtc = new DateTime(2026, 7, 10, 11, 59, 0, DateTimeKind.Utc);
        var interaction = new Interaction
        {
            ItemId = "int1",
            ActivityItemId = "act1",
            AgentId = "agent-1",
            AnsweredUtc = answeredUtc,
        }.RestorePersistedStatus(InteractionStatus.Ended);
        var queueItem = new QueueItem
        {
            ItemId = "queue-1",
            ActivityItemId = "act1",
            ReservationId = "res-1",
        }.RestorePersistedStatus(QueueItemStatus.Assigned);
        var reservation = new ActivityReservation
        {
            ItemId = "res-1",
            AgentId = "agent-1",
            ActivityItemId = "act1",
        }.RestorePersistedStatus(ReservationStatus.Accepted);
        var agent = new AgentProfile
        {
            ItemId = "agent-1",
            ActiveReservationId = "res-1",
            PresenceStatus = AgentPresenceStatus.WrapUp,
        };

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);

        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager.Setup(m => m.FindByInteractionIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync((CallSession)null);

        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.FindByActivityIdAsync("act1", It.IsAny<CancellationToken>())).ReturnsAsync(queueItem);

        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.GetActiveByActivityAsync("act1", It.IsAny<CancellationToken>())).ReturnsAsync([reservation]);

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("agent-1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var presenceManager = new Mock<IAgentPresenceManager>();

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc));

        var service = new ProviderVoiceOfferSynchronizationService(
            interactionManager.Object,
            callSessionManager.Object,
            queueItemManager.Object,
            reservationManager.Object,
            agentManager.Object,
            new Mock<IOmnichannelActivityManager>().Object,
            new FakeContactCenterWorkStateService(),
            CreateServiceProvider(presenceManager.Object),
            clock.Object,
            new Mock<Microsoft.Extensions.Logging.ILogger<ProviderVoiceOfferSynchronizationService>>().Object);

        // Act
        await service.ReconcileEndedOfferAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        queueItemManager.Verify(
            m => m.UpdateAsync(
                It.Is<QueueItem>(value => value.Status == QueueItemStatus.Completed && value.DequeuedUtc.HasValue),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
        reservationManager.Verify(
            m => m.UpdateAsync(
                It.Is<ActivityReservation>(value => value.Status == ReservationStatus.Canceled),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
        presenceManager.Verify(
            m => m.StartWrapUpAsync("agent-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static IServiceProvider CreateServiceProvider(IAgentPresenceManager presenceManager = null)
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(provider => provider.GetService(typeof(IAgentPresenceManager)))
            .Returns(presenceManager ?? new Mock<IAgentPresenceManager>().Object);

        return serviceProvider.Object;
    }
}
