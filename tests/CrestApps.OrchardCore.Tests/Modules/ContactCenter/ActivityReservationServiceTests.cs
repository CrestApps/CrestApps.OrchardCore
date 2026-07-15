using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

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
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1", PresenceStatus = AgentPresenceStatus.Available };
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
    public async Task ReserveAsync_WhenConcurrentTransitionWins_ThrowsAndAbortsScope()
    {
        // Arrange
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager
            .Setup(manager => manager.NewAsync(
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityReservation());
        var queueItem = new QueueItem
        {
            ItemId = "qi-1",
            QueueId = "q1",
            ActivityItemId = "act-1",
            Status = QueueItemStatus.Waiting,
        };
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(manager => manager.FindByIdAsync("qi-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueItem);
        var agent = new AgentProfile
        {
            ItemId = "a1",
            UserId = "u1",
            PresenceStatus = AgentPresenceStatus.Available,
        };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager
            .Setup(manager => manager.FindByIdAsync("act-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelActivity { ItemId = "act-1" });
        var session = new Mock<ISession>();
        session
            .Setup(service => service.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException(new Document()));
        var service = CreateService(
            reservationManager,
            queueItemManager,
            agentManager,
            new Mock<IActivityQueueManager>(),
            new Mock<IActivityQueueService>(),
            new Mock<IInteractionManager>(),
            activityManager,
            new Mock<IContactCenterEventPublisher>(),
            new Mock<ITelephonyService>(),
            session: session);

        // Act
        var exception = await Assert.ThrowsAsync<ConcurrencyException>(() =>
            service.ReserveAsync(
                queueItem,
                agent,
                30,
                TestContext.Current.CancellationToken));

        // Assert
        Assert.NotNull(exception);
        session.Verify(service => service.ResetAsync(), Times.Never);
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
    public async Task ReserveAsync_WhenAgentDisconnectsBeforeFinalTransition_AbortsWithoutReserving()
    {
        // Arrange
        var item = new QueueItem
        {
            ItemId = "qi-1",
            QueueId = "q1",
            ActivityItemId = "act-1",
            Status = QueueItemStatus.Waiting,
        };
        var agent = new AgentProfile
        {
            ItemId = "a1",
            UserId = "u1",
            PresenceStatus = AgentPresenceStatus.Available,
        };
        var reservationManager = new Mock<IActivityReservationManager>();
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(manager => manager.FindByIdAsync("qi-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        var availabilityService = new Mock<IAgentAvailabilityService>();
        availabilityService
            .Setup(service => service.GetAsync("a1", "q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentAvailability)null);
        var service = CreateService(
            reservationManager,
            queueItemManager,
            agentManager,
            new Mock<IActivityQueueManager>(),
            new Mock<IActivityQueueService>(),
            new Mock<IInteractionManager>(),
            new Mock<IOmnichannelActivityManager>(),
            new Mock<IContactCenterEventPublisher>(),
            new Mock<ITelephonyService>(),
            availabilityService: availabilityService);

        // Act
        var reservation = await service.ReserveAsync(item, agent, 30, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(reservation);
        reservationManager.Verify(
            manager => manager.CreateAsync(It.IsAny<ActivityReservation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReserveAsync_WhenAgentAlreadyHasActiveReservation_AbortsWithoutReserving()
    {
        // Arrange
        var reservationManager = new Mock<IActivityReservationManager>();
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(m => m.FindByIdAsync("qi-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueItem { ItemId = "qi-1", QueueId = "q1", ActivityItemId = "act-1", Status = QueueItemStatus.Waiting });

        var alreadyReservedAgent = new AgentProfile
        {
            ItemId = "a1",
            UserId = "u1",
            PresenceStatus = AgentPresenceStatus.Reserved,
            ActiveReservationId = "r-existing",
        };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(alreadyReservedAgent);

        var queueManager = new Mock<IActivityQueueManager>();
        var queueService = new Mock<IActivityQueueService>();
        var interactionManager = new Mock<IInteractionManager>();
        var activityManager = new Mock<IOmnichannelActivityManager>();
        var service = CreateService(reservationManager, queueItemManager, agentManager, queueManager, queueService, interactionManager, activityManager, new Mock<IContactCenterEventPublisher>(), new Mock<ITelephonyService>());

        var item = new QueueItem { ItemId = "qi-1", QueueId = "q1", ActivityItemId = "act-1" };
        var selectedAgent = new AgentProfile { ItemId = "a1", UserId = "u1", PresenceStatus = AgentPresenceStatus.Available };

        // Act
        var reservation = await service.ReserveAsync(item, selectedAgent, 30, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(reservation);
        reservationManager.Verify(m => m.CreateAsync(It.IsAny<ActivityReservation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReserveAsync_WhenAgentReservationLockIsHeldByAnotherQueue_AbortsWithoutReserving()
    {
        // Arrange
        var reservationManager = new Mock<IActivityReservationManager>();
        var queueItemManager = new Mock<IQueueItemManager>();
        var agentManager = new Mock<IAgentProfileManager>();
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(l => l.TryAcquireLockAsync("ContactCenterActivityReservation:act-2", It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((null, true));
        distributedLock
            .Setup(l => l.TryAcquireLockAsync("ContactCenterAgentReservation:a1", It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((null, false));
        var service = CreateService(
            reservationManager,
            queueItemManager,
            agentManager,
            new Mock<IActivityQueueManager>(),
            new Mock<IActivityQueueService>(),
            new Mock<IInteractionManager>(),
            new Mock<IOmnichannelActivityManager>(),
            new Mock<IContactCenterEventPublisher>(),
            new Mock<ITelephonyService>(),
            distributedLock);

        // Act
        var reservation = await service.ReserveAsync(
            new QueueItem { ItemId = "qi-2", QueueId = "q2", ActivityItemId = "act-2" },
            new AgentProfile { ItemId = "a1", UserId = "u1" },
            30,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(reservation);
        queueItemManager.Verify(
            manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        reservationManager.Verify(
            manager => manager.CreateAsync(It.IsAny<ActivityReservation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReserveAsync_WhenBreakWasGrantedAfterRoutingDecision_AbortsReservation()
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
        var reservation = await service.ReserveAsync(item, selectedAgent, 30, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(reservation);
        Assert.Equal(AgentPresenceStatus.Break, currentAgent.PresenceStatus);
        reservationManager.Verify(
            manager => manager.CreateAsync(It.IsAny<ActivityReservation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReserveAsync_WhenAgentReachedCapacityAfterRoutingDecision_AbortsReservation()
    {
        // Arrange
        var reservationManager = new Mock<IActivityReservationManager>();
        var queueItem = new QueueItem
        {
            ItemId = "qi-1",
            QueueId = "q1",
            ActivityItemId = "act-1",
            Status = QueueItemStatus.Waiting,
        };
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(manager => manager.FindByIdAsync("qi-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueItem);
        var agent = new AgentProfile
        {
            ItemId = "a1",
            UserId = "u1",
            PresenceStatus = AgentPresenceStatus.Available,
            MaxConcurrentInteractions = 1,
        };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.CountActiveByAgentAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var service = CreateService(
            reservationManager,
            queueItemManager,
            agentManager,
            new Mock<IActivityQueueManager>(),
            new Mock<IActivityQueueService>(),
            interactionManager,
            new Mock<IOmnichannelActivityManager>(),
            new Mock<IContactCenterEventPublisher>(),
            new Mock<ITelephonyService>());

        // Act
        var reservation = await service.ReserveAsync(
            queueItem,
            agent,
            30,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(reservation);
        reservationManager.Verify(
            manager => manager.CreateAsync(It.IsAny<ActivityReservation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExpireDueAsync_ReleasesPendingReservationsAndReturnsItemToQueue()
    {
        // Arrange
        var reservation = new ActivityReservation { ItemId = "r1", QueueItemId = "qi-1", AgentId = "a1", ActivityItemId = "act-1", Status = ReservationStatus.Pending };
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.ListExpiredAsync(_now, It.IsAny<CancellationToken>())).ReturnsAsync([reservation]);
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
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
    public async Task ExpireDueAsync_WhenReservationWasAcceptedAfterExpiredListWasRead_DoesNotReleaseIt()
    {
        // Arrange
        var candidate = new ActivityReservation
        {
            ItemId = "r1",
            QueueItemId = "qi-1",
            AgentId = "a1",
            ActivityItemId = "act-1",
            Status = ReservationStatus.Pending,
            ExpiresUtc = _now.AddSeconds(-1),
        };
        var accepted = new ActivityReservation
        {
            ItemId = "r1",
            QueueItemId = "qi-1",
            AgentId = "a1",
            ActivityItemId = "act-1",
            Status = ReservationStatus.Accepted,
            ExpiresUtc = candidate.ExpiresUtc,
        };
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.ListExpiredAsync(_now, It.IsAny<CancellationToken>())).ReturnsAsync([candidate]);
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(accepted);
        var queueItemManager = new Mock<IQueueItemManager>();
        var service = CreateService(
            reservationManager,
            queueItemManager,
            new Mock<IAgentProfileManager>(),
            new Mock<IActivityQueueManager>(),
            new Mock<IActivityQueueService>(),
            new Mock<IInteractionManager>(),
            new Mock<IOmnichannelActivityManager>(),
            new Mock<IContactCenterEventPublisher>(),
            new Mock<ITelephonyService>());

        // Act
        var count = await service.ExpireDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, count);
        Assert.Equal(ReservationStatus.Accepted, accepted.Status);
        reservationManager.Verify(
            manager => manager.UpdateAsync(It.IsAny<ActivityReservation>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Never);
        queueItemManager.Verify(
            manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExpireDueAsync_WhenQueueItemHasNewerReservation_DoesNotResetCurrentOffer()
    {
        // Arrange
        var reservation = new ActivityReservation
        {
            ItemId = "r-old",
            QueueItemId = "qi-1",
            AgentId = "a1",
            ActivityItemId = "act-1",
            Status = ReservationStatus.Pending,
            ExpiresUtc = _now.AddSeconds(-1),
        };
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.ListExpiredAsync(_now, It.IsAny<CancellationToken>())).ReturnsAsync([reservation]);
        reservationManager.Setup(m => m.FindByIdAsync("r-old", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);

        var queueItem = new QueueItem
        {
            ItemId = "qi-1",
            ActivityItemId = "act-1",
            Status = QueueItemStatus.Reserved,
            ReservationId = "r-new",
            AgentId = "a2",
        };
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.FindByIdAsync("qi-1", It.IsAny<CancellationToken>())).ReturnsAsync(queueItem);

        var agent = new AgentProfile
        {
            ItemId = "a1",
            PresenceStatus = AgentPresenceStatus.Reserved,
            ActiveReservationId = "r-old",
        };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(manager => manager.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var interactionManager = new Mock<IInteractionManager>();
        var activityManager = new Mock<IOmnichannelActivityManager>();
        var service = CreateService(
            reservationManager,
            queueItemManager,
            agentManager,
            new Mock<IActivityQueueManager>(),
            new Mock<IActivityQueueService>(),
            interactionManager,
            activityManager,
            new Mock<IContactCenterEventPublisher>(),
            new Mock<ITelephonyService>());

        // Act
        var count = await service.ExpireDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, count);
        Assert.Equal(ReservationStatus.Expired, reservation.Status);
        Assert.Equal(QueueItemStatus.Reserved, queueItem.Status);
        Assert.Equal("r-new", queueItem.ReservationId);
        Assert.Equal(AgentPresenceStatus.Offline, agent.PresenceStatus);
        Assert.Null(agent.ActiveReservationId);
        agentManager.Verify(
            manager => manager.UpdateAsync(agent, It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Once);
        interactionManager.Verify(
            manager => manager.FindByActivityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        activityManager.Verify(
            manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExpireDueAsync_WhenRequeueing_ClearsStaleRingingAssignmentFromInteraction()
    {
        // Arrange
        var reservation = new ActivityReservation { ItemId = "r1", QueueId = "q1", QueueItemId = "qi-1", AgentId = "a1", ActivityItemId = "act-1", Status = ReservationStatus.Pending };
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.ListExpiredAsync(_now, It.IsAny<CancellationToken>())).ReturnsAsync([reservation]);
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
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
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
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
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
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
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
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
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
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

    [Fact]
    public async Task CompensateAsync_ReleasesAssignmentWithoutOverwritingFailedWorkState()
    {
        // Arrange
        var reservation = new ActivityReservation
        {
            ItemId = "r1",
            QueueItemId = "qi-1",
            AgentId = "a1",
            ActivityItemId = "act-1",
            Status = ReservationStatus.Accepted,
        };
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager
            .Setup(manager => manager.FindByIdAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        reservationManager
            .Setup(manager => manager.ListActiveByAgentAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var queueItem = new QueueItem
        {
            ItemId = "qi-1",
            Status = QueueItemStatus.Assigned,
            ReservationId = "r1",
            AgentId = "a1",
        };
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(manager => manager.FindByIdAsync("qi-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueItem);
        var agent = new AgentProfile
        {
            ItemId = "a1",
            PresenceStatus = AgentPresenceStatus.Busy,
            RequestedPresenceStatus = AgentPresenceStatus.Available,
        };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        var activity = new OmnichannelActivity
        {
            ItemId = "act-1",
            Status = ActivityStatus.Failed,
            AssignmentStatus = ActivityAssignmentStatus.Assigned,
            ReservationId = "r1",
            AssignedToId = "u1",
        };
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager
            .Setup(manager => manager.FindByIdAsync("act-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(activity);
        var queueService = new Mock<IActivityQueueService>();
        queueService
            .Setup(service => service.DequeueAsync(
                queueItem,
                QueueItemStatus.Removed,
                It.IsAny<CancellationToken>()))
            .Callback(() => queueItem.Status = QueueItemStatus.Removed);
        var interactionManager = new Mock<IInteractionManager>();
        var service = CreateService(
            reservationManager,
            queueItemManager,
            agentManager,
            new Mock<IActivityQueueManager>(),
            queueService,
            interactionManager,
            activityManager,
            new Mock<IContactCenterEventPublisher>(),
            new Mock<ITelephonyService>());

        // Act
        var compensated = await service.CompensateAsync(
            "r1",
            removeFromQueue: true,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(reservation, compensated);
        Assert.Equal(ReservationStatus.Canceled, reservation.Status);
        Assert.Equal(QueueItemStatus.Removed, queueItem.Status);
        Assert.Null(queueItem.ReservationId);
        Assert.Null(queueItem.AgentId);
        Assert.Equal(AgentPresenceStatus.Available, agent.PresenceStatus);
        Assert.Equal(ActivityStatus.Failed, activity.Status);
        Assert.Equal(ActivityAssignmentStatus.Released, activity.AssignmentStatus);
        Assert.Null(activity.ReservationId);
        Assert.Null(activity.AssignedToId);
        interactionManager.Verify(
            manager => manager.UpdateAsync(
                It.IsAny<Interaction>(),
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CompensateAsync_WhenReservationIsPending_ReleasesReservedAgentOwnership()
    {
        // Arrange
        var reservation = new ActivityReservation
        {
            ItemId = "r1",
            QueueItemId = "qi-1",
            AgentId = "a1",
            ActivityItemId = "act-1",
            Status = ReservationStatus.Pending,
        };
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager
            .Setup(manager => manager.FindByIdAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        reservationManager
            .Setup(manager => manager.ListActiveByAgentAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var queueItem = new QueueItem
        {
            ItemId = "qi-1",
            Status = QueueItemStatus.Reserved,
            ReservationId = "r1",
            AgentId = "a1",
        };
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(manager => manager.FindByIdAsync("qi-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueItem);
        var agent = new AgentProfile
        {
            ItemId = "a1",
            PresenceStatus = AgentPresenceStatus.Reserved,
            RequestedPresenceStatus = AgentPresenceStatus.Available,
            ActiveReservationId = "r1",
        };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        var activity = new OmnichannelActivity
        {
            ItemId = "act-1",
            Status = ActivityStatus.Pending,
            AssignmentStatus = ActivityAssignmentStatus.Reserved,
            ReservationId = "r1",
        };
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager
            .Setup(manager => manager.FindByIdAsync("act-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(activity);
        var service = CreateService(
            reservationManager,
            queueItemManager,
            agentManager,
            new Mock<IActivityQueueManager>(),
            new Mock<IActivityQueueService>(),
            new Mock<IInteractionManager>(),
            activityManager,
            new Mock<IContactCenterEventPublisher>(),
            new Mock<ITelephonyService>());

        // Act
        var compensated = await service.CompensateAsync(
            "r1",
            removeFromQueue: false,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(reservation, compensated);
        Assert.Equal(ReservationStatus.Canceled, reservation.Status);
        Assert.Equal(QueueItemStatus.Waiting, queueItem.Status);
        Assert.Equal(AgentPresenceStatus.Available, agent.PresenceStatus);
        Assert.Null(agent.RequestedPresenceStatus);
        Assert.Null(agent.ActiveReservationId);
        Assert.Equal(ActivityAssignmentStatus.Available, activity.AssignmentStatus);
        Assert.Null(activity.ReservationId);
    }

    [Fact]
    public async Task CompensateAsync_WhenWorkHasNewOwner_DoesNotReleaseCurrentAssignment()
    {
        // Arrange
        var reservation = new ActivityReservation
        {
            ItemId = "r1",
            QueueItemId = "qi-1",
            AgentId = "a1",
            ActivityItemId = "act-1",
            Status = ReservationStatus.Accepted,
        };
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager
            .Setup(manager => manager.FindByIdAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        reservationManager
            .Setup(manager => manager.ListActiveByAgentAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ActivityReservation
                {
                    ItemId = "r2",
                    AgentId = "a1",
                    Status = ReservationStatus.Accepted,
                },
            ]);
        var queueItem = new QueueItem
        {
            ItemId = "qi-1",
            Status = QueueItemStatus.Assigned,
            ReservationId = "r2",
            AgentId = "a2",
        };
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(manager => manager.FindByIdAsync("qi-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueItem);
        var agent = new AgentProfile
        {
            ItemId = "a1",
            PresenceStatus = AgentPresenceStatus.Busy,
        };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        var activity = new OmnichannelActivity
        {
            ItemId = "act-1",
            Status = ActivityStatus.Dialing,
            AssignmentStatus = ActivityAssignmentStatus.Assigned,
            ReservationId = "r2",
            AssignedToId = "u2",
        };
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager
            .Setup(manager => manager.FindByIdAsync("act-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(activity);
        var service = CreateService(
            reservationManager,
            queueItemManager,
            agentManager,
            new Mock<IActivityQueueManager>(),
            new Mock<IActivityQueueService>(),
            new Mock<IInteractionManager>(),
            activityManager,
            new Mock<IContactCenterEventPublisher>(),
            new Mock<ITelephonyService>());

        // Act
        var compensated = await service.CompensateAsync(
            "r1",
            removeFromQueue: true,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(reservation, compensated);
        Assert.Equal(ReservationStatus.Canceled, reservation.Status);
        Assert.Equal(QueueItemStatus.Assigned, queueItem.Status);
        Assert.Equal("r2", queueItem.ReservationId);
        Assert.Equal("a2", queueItem.AgentId);
        Assert.Equal(AgentPresenceStatus.Busy, agent.PresenceStatus);
        Assert.Null(agent.ActiveReservationId);
        Assert.Equal(ActivityStatus.Dialing, activity.Status);
        Assert.Equal(ActivityAssignmentStatus.Assigned, activity.AssignmentStatus);
        Assert.Equal("r2", activity.ReservationId);
        Assert.Equal("u2", activity.AssignedToId);
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
        Mock<ITelephonyService> telephonyService,
        Mock<IDistributedLock> distributedLock = null,
        Mock<ISession> session = null,
        Mock<IAgentAvailabilityService> availabilityService = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        if (distributedLock is null)
        {
            distributedLock = new Mock<IDistributedLock>();
            distributedLock
                .Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
                .ReturnsAsync((null, true));
        }

        if (session is null)
        {
            session = new Mock<ISession>();
            session
                .Setup(service => service.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        if (availabilityService is null)
        {
            availabilityService = new Mock<IAgentAvailabilityService>();
            availabilityService
                .Setup(service => service.GetAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((string agentId, string _, CancellationToken _) => new AgentAvailability
                {
                    Agent = new AgentProfile { ItemId = agentId },
                });
        }

        return new ActivityReservationService(
            reservationManager.Object,
            queueItemManager.Object,
            agentManager.Object,
            availabilityService.Object,
            queueManager.Object,
            queueService.Object,
            interactionManager.Object,
            activityManager.Object,
            publisher.Object,
            [telephonyService.Object],
            distributedLock.Object,
            session.Object,
            clock.Object,
            new Mock<ILogger<ActivityReservationService>>().Object);
    }
}
