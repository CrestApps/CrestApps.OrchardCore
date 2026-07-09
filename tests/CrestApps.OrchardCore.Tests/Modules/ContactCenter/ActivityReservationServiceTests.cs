using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ActivityReservationServiceTests
{
    private static readonly DateTime _now = new(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ReserveAsync_SetsReservedStateAndPublishesEvents()
    {
        // Arrange
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ActivityReservation());
        var queueItemManager = new Mock<IQueueItemManager>();
        var agentManager = new Mock<IAgentProfileManager>();
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager.Setup(m => m.FindByIdAsync("q1", It.IsAny<CancellationToken>())).ReturnsAsync(new ActivityQueue { ItemId = "q1" });
        var queueService = new Mock<IActivityQueueService>();
        var interactionManager = new Mock<IInteractionManager>();
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.FindByIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(new OmnichannelActivity { ItemId = "act-1" });
        var publisher = new Mock<IContactCenterEventPublisher>();
        var service = CreateService(reservationManager, queueItemManager, agentManager, queueManager, queueService, interactionManager, activityManager, publisher, new Mock<ITelephonyService>());

        var item = new QueueItem { ItemId = "qi-1", QueueId = "q1", ActivityItemId = "act-1" };
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1" };
        queueItemManager.Setup(m => m.FindByIdAsync("qi-1", It.IsAny<CancellationToken>())).ReturnsAsync(item);

        // Act
        var reservation = await service.ReserveAsync(item, agent, 30, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReservationStatus.Pending, reservation.Status);
        Assert.Equal(QueueItemStatus.Reserved, item.Status);
        Assert.Equal(AgentPresenceStatus.Reserved, agent.PresenceStatus);
        publisher.Verify(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ReserveAsync_WhenItemNoLongerWaiting_AbortsWithoutReserving()
    {
        // Arrange
        var reservationManager = new Mock<IActivityReservationManager>();
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(m => m.FindByIdAsync("qi-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueItem { ItemId = "qi-1", Status = QueueItemStatus.Reserved });
        var agentManager = new Mock<IAgentProfileManager>();
        var queueManager = new Mock<IActivityQueueManager>();
        var queueService = new Mock<IActivityQueueService>();
        var interactionManager = new Mock<IInteractionManager>();
        var activityManager = new Mock<IOmnichannelActivityManager>();
        var service = CreateService(reservationManager, queueItemManager, agentManager, queueManager, queueService, interactionManager, activityManager, new Mock<IContactCenterEventPublisher>(), new Mock<ITelephonyService>());

        var staleItem = new QueueItem { ItemId = "qi-1", QueueId = "q1", ActivityItemId = "act-1" };
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1" };

        // Act
        var reservation = await service.ReserveAsync(staleItem, agent, 30, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(reservation);
        reservationManager.Verify(m => m.CreateAsync(It.IsAny<ActivityReservation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReserveAsync_WhenBreakWasGrantedAfterRoutingDecision_PreservesPendingBreak()
    {
        // Arrange
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ActivityReservation());
        var queueItemManager = new Mock<IQueueItemManager>();
        var currentAgent = new AgentProfile { ItemId = "a1", UserId = "u1", PresenceStatus = AgentPresenceStatus.Break };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(currentAgent);
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager.Setup(m => m.FindByIdAsync("q1", It.IsAny<CancellationToken>())).ReturnsAsync(new ActivityQueue { ItemId = "q1" });
        var queueService = new Mock<IActivityQueueService>();
        var interactionManager = new Mock<IInteractionManager>();
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.FindByIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(new OmnichannelActivity { ItemId = "act-1" });
        var service = CreateService(reservationManager, queueItemManager, agentManager, queueManager, queueService, interactionManager, activityManager, new Mock<IContactCenterEventPublisher>(), new Mock<ITelephonyService>());

        var item = new QueueItem { ItemId = "qi-1", QueueId = "q1", ActivityItemId = "act-1" };
        var selectedAgent = new AgentProfile { ItemId = "a1", UserId = "u1", PresenceStatus = AgentPresenceStatus.Available };
        queueItemManager.Setup(m => m.FindByIdAsync("qi-1", It.IsAny<CancellationToken>())).ReturnsAsync(item);

        // Act
        await service.ReserveAsync(item, selectedAgent, 30, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.Reserved, currentAgent.PresenceStatus);
        Assert.Equal(AgentPresenceStatus.Break, currentAgent.RequestedPresenceStatus);
    }

    [Fact]
    public async Task ExpireDueAsync_ReleasesPendingReservationsAndReturnsItemToQueue()
    {
        // Arrange
        var reservation = new ActivityReservation { ItemId = "r1", QueueItemId = "qi-1", AgentId = "a1", ActivityItemId = "act-1", Status = ReservationStatus.Pending };
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.ListExpiredAsync(_now, It.IsAny<CancellationToken>())).ReturnsAsync([reservation]);
        var queueItem = new QueueItem { ItemId = "qi-1", Status = QueueItemStatus.Reserved };
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.FindByIdAsync("qi-1", It.IsAny<CancellationToken>())).ReturnsAsync(queueItem);
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(new AgentProfile { ItemId = "a1" });
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager.Setup(m => m.FindByIdAsync("q1", It.IsAny<CancellationToken>())).ReturnsAsync(new ActivityQueue { ItemId = "q1" });
        var queueService = new Mock<IActivityQueueService>();
        var interaction = new Interaction { ItemId = "i1", ActivityItemId = "act-1", AgentId = "a1", Status = InteractionStatus.Ringing };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByActivityIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.FindByIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(new OmnichannelActivity { ItemId = "act-1" });
        var service = CreateService(reservationManager, queueItemManager, agentManager, queueManager, queueService, interactionManager, activityManager, new Mock<IContactCenterEventPublisher>(), new Mock<ITelephonyService>());

        // Act
        var count = await service.ExpireDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, count);
        Assert.Equal(ReservationStatus.Expired, reservation.Status);
        Assert.Equal(QueueItemStatus.Waiting, queueItem.Status);
        Assert.Equal(InteractionStatus.Created, interaction.Status);
        Assert.Null(interaction.AgentId);
    }

    [Fact]
    public async Task ExpireDueAsync_WhenRequeueing_ClearsStaleRingingAssignmentFromInteraction()
    {
        // Arrange
        var reservation = new ActivityReservation { ItemId = "r1", QueueId = "q1", QueueItemId = "qi-1", AgentId = "a1", ActivityItemId = "act-1", Status = ReservationStatus.Pending };
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.ListExpiredAsync(_now, It.IsAny<CancellationToken>())).ReturnsAsync([reservation]);
        var queueItem = new QueueItem { ItemId = "qi-1", QueueId = "q1", Status = QueueItemStatus.Reserved, ReservationId = "r1", AgentId = "a1" };
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.FindByIdAsync("qi-1", It.IsAny<CancellationToken>())).ReturnsAsync(queueItem);
        var agent = new AgentProfile { ItemId = "a1", PresenceStatus = AgentPresenceStatus.Available, QueueIds = ["q1"] };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager.Setup(m => m.FindByIdAsync("q1", It.IsAny<CancellationToken>())).ReturnsAsync(new ActivityQueue { ItemId = "q1", Name = "Voice" });
        var queueService = new Mock<IActivityQueueService>();
        var interaction = new Interaction { ItemId = "i1", ActivityItemId = "act-1", AgentId = "a1", Status = InteractionStatus.Ringing };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByActivityIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.FindByIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(new OmnichannelActivity { ItemId = "act-1" });
        var service = CreateService(reservationManager, queueItemManager, agentManager, queueManager, queueService, interactionManager, activityManager, new Mock<IContactCenterEventPublisher>(), new Mock<ITelephonyService>());

        // Act
        await service.ExpireDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionStatus.Created, interaction.Status);
        Assert.Null(interaction.AgentId);
        interactionManager.Verify(m => m.UpdateAsync(interaction, It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExpireDueAsync_WhenBreakIsPending_GrantsBreak()
    {
        // Arrange
        var reservation = new ActivityReservation { ItemId = "r1", QueueItemId = "qi-1", AgentId = "a1", ActivityItemId = "act-1", Status = ReservationStatus.Pending };
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.ListExpiredAsync(_now, It.IsAny<CancellationToken>())).ReturnsAsync([reservation]);
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.FindByIdAsync("qi-1", It.IsAny<CancellationToken>())).ReturnsAsync(new QueueItem { ItemId = "qi-1", Status = QueueItemStatus.Reserved });
        var agent = new AgentProfile { ItemId = "a1", RequestedPresenceStatus = AgentPresenceStatus.Break };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager.Setup(m => m.FindByIdAsync("q1", It.IsAny<CancellationToken>())).ReturnsAsync(new ActivityQueue { ItemId = "q1" });
        var queueService = new Mock<IActivityQueueService>();
        var interactionManager = new Mock<IInteractionManager>();
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.FindByIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(new OmnichannelActivity { ItemId = "act-1" });
        var service = CreateService(reservationManager, queueItemManager, agentManager, queueManager, queueService, interactionManager, activityManager, new Mock<IContactCenterEventPublisher>(), new Mock<ITelephonyService>());

        // Act
        await service.ExpireDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.Break, agent.PresenceStatus);
        Assert.Null(agent.RequestedPresenceStatus);
    }

    [Fact]
    public async Task ExpireDueAsync_WhenAgentSignedOut_KeepsAgentOffline()
    {
        // Arrange
        var reservation = new ActivityReservation { ItemId = "r1", QueueItemId = "qi-1", AgentId = "a1", ActivityItemId = "act-1", Status = ReservationStatus.Pending };
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.ListExpiredAsync(_now, It.IsAny<CancellationToken>())).ReturnsAsync([reservation]);
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.FindByIdAsync("qi-1", It.IsAny<CancellationToken>())).ReturnsAsync(new QueueItem { ItemId = "qi-1", Status = QueueItemStatus.Reserved });
        var agent = new AgentProfile { ItemId = "a1", PresenceStatus = AgentPresenceStatus.Offline };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager.Setup(m => m.FindByIdAsync("q1", It.IsAny<CancellationToken>())).ReturnsAsync(new ActivityQueue { ItemId = "q1" });
        var queueService = new Mock<IActivityQueueService>();
        var interactionManager = new Mock<IInteractionManager>();
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.FindByIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(new OmnichannelActivity { ItemId = "act-1" });
        var service = CreateService(reservationManager, queueItemManager, agentManager, queueManager, queueService, interactionManager, activityManager, new Mock<IContactCenterEventPublisher>(), new Mock<ITelephonyService>());

        // Act
        await service.ExpireDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.Offline, agent.PresenceStatus);
        Assert.Null(agent.RequestedPresenceStatus);
    }

    [Fact]
    public async Task ExpireDueAsync_WhenQueueUsesVoicemail_RemovesItemAndSendsToVoicemail()
    {
        // Arrange
        var reservation = new ActivityReservation { ItemId = "r1", QueueId = "q1", QueueItemId = "qi-1", AgentId = "a1", ActivityItemId = "act-1", Status = ReservationStatus.Pending };
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.ListExpiredAsync(_now, It.IsAny<CancellationToken>())).ReturnsAsync([reservation]);
        var queueItem = new QueueItem { ItemId = "qi-1", QueueId = "q1", Status = QueueItemStatus.Reserved, ReservationId = "r1", AgentId = "a1" };
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.FindByIdAsync("qi-1", It.IsAny<CancellationToken>())).ReturnsAsync(queueItem);
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1", UserName = "agent", DisplayName = "Agent" };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager.Setup(m => m.FindByIdAsync("q1", It.IsAny<CancellationToken>())).ReturnsAsync(new ActivityQueue { ItemId = "q1", Name = "Voice", UnansweredOfferAction = UnansweredOfferAction.Voicemail });
        var queueService = new Mock<IActivityQueueService>();
        var interaction = new Interaction { ItemId = "i1", ActivityItemId = "act-1", ProviderInteractionId = "call-1" };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByActivityIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.FindByIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(new OmnichannelActivity { ItemId = "act-1" });
        var telephonyService = new Mock<ITelephonyService>();
        telephonyService.Setup(m => m.SendToVoicemailAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>())).ReturnsAsync(TelephonyResult.Success());
        var service = CreateService(reservationManager, queueItemManager, agentManager, queueManager, queueService, interactionManager, activityManager, new Mock<IContactCenterEventPublisher>(), telephonyService);

        // Act
        await service.ExpireDueAsync(TestContext.Current.CancellationToken);

        // Assert
        queueService.Verify(m => m.DequeueAsync(It.Is<QueueItem>(item => item.ItemId == "qi-1"), QueueItemStatus.Removed, It.IsAny<CancellationToken>()), Times.Once);
        telephonyService.Verify(m => m.SendToVoicemailAsync(It.Is<CallReference>(call => call.CallId == "call-1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExpireDueAsync_WhenQueueUsesReject_RemovesItemAndRejectsCall()
    {
        // Arrange
        var reservation = new ActivityReservation { ItemId = "r1", QueueId = "q1", QueueItemId = "qi-1", AgentId = "a1", ActivityItemId = "act-1", Status = ReservationStatus.Pending };
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.ListExpiredAsync(_now, It.IsAny<CancellationToken>())).ReturnsAsync([reservation]);
        var queueItem = new QueueItem { ItemId = "qi-1", QueueId = "q1", Status = QueueItemStatus.Reserved, ReservationId = "r1", AgentId = "a1" };
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.FindByIdAsync("qi-1", It.IsAny<CancellationToken>())).ReturnsAsync(queueItem);
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1" };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager.Setup(m => m.FindByIdAsync("q1", It.IsAny<CancellationToken>())).ReturnsAsync(new ActivityQueue { ItemId = "q1", Name = "Voice", UnansweredOfferAction = UnansweredOfferAction.Reject });
        var queueService = new Mock<IActivityQueueService>();
        var interaction = new Interaction { ItemId = "i1", ActivityItemId = "act-1", ProviderInteractionId = "call-1" };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByActivityIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.FindByIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(new OmnichannelActivity { ItemId = "act-1" });
        var telephonyService = new Mock<ITelephonyService>();
        telephonyService.Setup(m => m.RejectAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>())).ReturnsAsync(TelephonyResult.Success());
        var service = CreateService(reservationManager, queueItemManager, agentManager, queueManager, queueService, interactionManager, activityManager, new Mock<IContactCenterEventPublisher>(), telephonyService);

        // Act
        await service.ExpireDueAsync(TestContext.Current.CancellationToken);

        // Assert
        queueService.Verify(m => m.DequeueAsync(It.Is<QueueItem>(item => item.ItemId == "qi-1"), QueueItemStatus.Removed, It.IsAny<CancellationToken>()), Times.Once);
        telephonyService.Verify(m => m.RejectAsync(It.Is<CallReference>(call => call.CallId == "call-1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_ReleasesPendingReservationAndMarksCanceled()
    {
        // Arrange
        var reservation = new ActivityReservation { ItemId = "r1", QueueItemId = "qi-1", AgentId = "a1", ActivityItemId = "act-1", Status = ReservationStatus.Pending };
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
        var queueItem = new QueueItem { ItemId = "qi-1", Status = QueueItemStatus.Reserved };
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.FindByIdAsync("qi-1", It.IsAny<CancellationToken>())).ReturnsAsync(queueItem);
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(new AgentProfile { ItemId = "a1" });
        var queueManager = new Mock<IActivityQueueManager>();
        var queueService = new Mock<IActivityQueueService>();
        var interactionManager = new Mock<IInteractionManager>();
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.FindByIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(new OmnichannelActivity { ItemId = "act-1" });
        var service = CreateService(reservationManager, queueItemManager, agentManager, queueManager, queueService, interactionManager, activityManager, new Mock<IContactCenterEventPublisher>(), new Mock<ITelephonyService>());

        // Act
        var canceled = await service.CancelAsync("r1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(reservation, canceled);
        Assert.Equal(ReservationStatus.Canceled, reservation.Status);
        Assert.Equal(QueueItemStatus.Waiting, queueItem.Status);
    }

    private static ActivityReservationService CreateService(
        Mock<IActivityReservationManager> reservationManager,
        Mock<IQueueItemManager> queueItemManager,
        Mock<IAgentProfileManager> agentManager,
        Mock<IActivityQueueManager> queueManager,
        Mock<IActivityQueueService> queueService,
        Mock<IInteractionManager> interactionManager,
        Mock<IOmnichannelActivityManager> activityManager,
        Mock<IContactCenterEventPublisher> publisher,
        Mock<ITelephonyService> telephonyService)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new ActivityReservationService(
            reservationManager.Object,
            queueItemManager.Object,
            agentManager.Object,
            queueManager.Object,
            queueService.Object,
            interactionManager.Object,
            activityManager.Object,
            publisher.Object,
            [telephonyService.Object],
            clock.Object,
            new Mock<ILogger<ActivityReservationService>>().Object);
    }
}
