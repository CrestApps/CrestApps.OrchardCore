using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
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
            Status = InteractionStatus.Ended,
        };
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "int1",
            ActivityItemId = "act1",
            AgentId = "agent-1",
            State = ContactCenterCallState.Ended,
        };
        var queueItem = new QueueItem
        {
            ItemId = "queue-1",
            ActivityItemId = "act1",
            ReservationId = "res-1",
            Status = QueueItemStatus.Reserved,
        };
        var reservation = new ActivityReservation
        {
            ItemId = "res-1",
            AgentId = "agent-1",
            ActivityItemId = "act1",
            Status = ReservationStatus.Pending,
        };
        var agent = new AgentProfile
        {
            ItemId = "agent-1",
            ActiveReservationId = "res-1",
            PresenceStatus = AgentPresenceStatus.Reserved,
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
        reservationManager.Setup(m => m.FindByIdAsync("res-1", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);

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
    public async Task ReconcileEndedOfferAsync_WhenAnsweredCallEnded_CompletesAssignedQueueItem()
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "int1",
            ActivityItemId = "act1",
            AgentId = "agent-1",
            Status = InteractionStatus.Ended,
            AnsweredUtc = new DateTime(2026, 7, 10, 11, 59, 0, DateTimeKind.Utc),
        };
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "int1",
            ActivityItemId = "act1",
            AgentId = "agent-1",
            State = ContactCenterCallState.Ended,
            AnsweredUtc = interaction.AnsweredUtc,
        };
        var queueItem = new QueueItem
        {
            ItemId = "queue-1",
            ActivityItemId = "act1",
            ReservationId = "res-1",
            Status = QueueItemStatus.Assigned,
        };

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);

        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager.Setup(m => m.FindByInteractionIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(session);

        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.FindByActivityIdAsync("act1", It.IsAny<CancellationToken>())).ReturnsAsync(queueItem);

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc));

        var reservationManager = new Mock<IActivityReservationManager>();
        var agentManager = new Mock<IAgentProfileManager>();
        var activityManager = new Mock<IOmnichannelActivityManager>();
        var service = new ProviderVoiceOfferSynchronizationService(
            interactionManager.Object,
            callSessionManager.Object,
            queueItemManager.Object,
            reservationManager.Object,
            agentManager.Object,
            activityManager.Object,
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
            Status = InteractionStatus.Transferring,
            AnsweredUtc = answeredUtc,
        };
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "int1",
            ActivityItemId = "act1",
            AgentId = "agent-1",
            State = ContactCenterCallState.Transferred,
            AnsweredUtc = answeredUtc,
        };
        var queueItem = new QueueItem
        {
            ItemId = "queue-1",
            ActivityItemId = "act1",
            ReservationId = "res-1",
            Status = QueueItemStatus.Assigned,
        };

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);

        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager.Setup(m => m.FindByInteractionIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(session);

        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.FindByActivityIdAsync("act1", It.IsAny<CancellationToken>())).ReturnsAsync(queueItem);

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc));

        var service = new ProviderVoiceOfferSynchronizationService(
            interactionManager.Object,
            callSessionManager.Object,
            queueItemManager.Object,
            new Mock<IActivityReservationManager>().Object,
            new Mock<IAgentProfileManager>().Object,
            new Mock<IOmnichannelActivityManager>().Object,
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
}
