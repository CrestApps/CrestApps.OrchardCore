using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
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
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.FindByIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(new OmnichannelActivity { ItemId = "act-1" });
        var publisher = new Mock<IContactCenterEventPublisher>();
        var service = CreateService(reservationManager, queueItemManager, agentManager, activityManager, publisher);

        var item = new QueueItem { ItemId = "qi-1", QueueId = "q1", ActivityItemId = "act-1" };
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1" };

        // Act
        var reservation = await service.ReserveAsync(item, agent, 30, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReservationStatus.Pending, reservation.Status);
        Assert.Equal(QueueItemStatus.Reserved, item.Status);
        Assert.Equal(AgentPresenceStatus.Reserved, agent.PresenceStatus);
        publisher.Verify(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
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
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.FindByIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(new OmnichannelActivity { ItemId = "act-1" });
        var service = CreateService(reservationManager, queueItemManager, agentManager, activityManager, new Mock<IContactCenterEventPublisher>());

        var item = new QueueItem { ItemId = "qi-1", QueueId = "q1", ActivityItemId = "act-1" };
        var selectedAgent = new AgentProfile { ItemId = "a1", UserId = "u1", PresenceStatus = AgentPresenceStatus.Available };

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
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.FindByIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(new OmnichannelActivity { ItemId = "act-1" });
        var service = CreateService(reservationManager, queueItemManager, agentManager, activityManager, new Mock<IContactCenterEventPublisher>());

        // Act
        var count = await service.ExpireDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, count);
        Assert.Equal(ReservationStatus.Expired, reservation.Status);
        Assert.Equal(QueueItemStatus.Waiting, queueItem.Status);
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
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.FindByIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(new OmnichannelActivity { ItemId = "act-1" });
        var service = CreateService(reservationManager, queueItemManager, agentManager, activityManager, new Mock<IContactCenterEventPublisher>());

        // Act
        await service.ExpireDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.Break, agent.PresenceStatus);
        Assert.Null(agent.RequestedPresenceStatus);
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
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.FindByIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(new OmnichannelActivity { ItemId = "act-1" });
        var service = CreateService(reservationManager, queueItemManager, agentManager, activityManager, new Mock<IContactCenterEventPublisher>());

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
        Mock<IOmnichannelActivityManager> activityManager,
        Mock<IContactCenterEventPublisher> publisher)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new ActivityReservationService(
            reservationManager.Object,
            queueItemManager.Object,
            agentManager.Object,
            activityManager.Object,
            publisher.Object,
            clock.Object);
    }
}
