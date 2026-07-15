using System.Text.Json;
#nullable enable annotations

using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
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
            manager => manager.UpdateAsync(
                It.IsAny<ActivityReservation>(),
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()),
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
            manager => manager.UpdateAsync(
                agent,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()),
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
        interactionManager.Verify(
            m => m.UpdateAsync(
                interaction,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
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
    public async Task ExpireDueAsync_WhenQueueUsesVoicemail_RegistersVoiceActionAndSchedulesDispatch()
    {
        // Arrange
        var order = new List<string>();
        var reservation = new ActivityReservation { ItemId = "r1", QueueId = "q1", QueueItemId = "qi-1", AgentId = "a1", ActivityItemId = "act-1", Status = ReservationStatus.Pending };
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.ListExpiredAsync(_now, It.IsAny<CancellationToken>())).ReturnsAsync([reservation]);
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
        reservationManager
            .Setup(m => m.UpdateAsync(
                reservation,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("reservation-update"))
            .Returns(ValueTask.CompletedTask);
        var queueItem = new QueueItem { ItemId = "qi-1", QueueId = "q1", Status = QueueItemStatus.Reserved, ReservationId = "r1", AgentId = "a1" };
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.FindByIdAsync("qi-1", It.IsAny<CancellationToken>())).ReturnsAsync(queueItem);
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1", UserName = "agent", DisplayName = "Agent", QueueIds = ["q1"] };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        agentManager
            .Setup(m => m.UpdateAsync(
                agent,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("agent-update"))
            .Returns(ValueTask.CompletedTask);
        var queue = new ActivityQueue { ItemId = "q1", Name = "Voice", UnansweredOfferAction = UnansweredOfferAction.Voicemail };
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager.Setup(m => m.FindByIdAsync("q1", It.IsAny<CancellationToken>())).ReturnsAsync(queue);
        var queueService = new Mock<IActivityQueueService>();
        queueService
            .Setup(m => m.DequeueAsync(queueItem, QueueItemStatus.Removed, It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                order.Add("queue-dequeue");
                queueItem.Status = QueueItemStatus.Removed;
            })
            .Returns(Task.CompletedTask);
        var interaction = new Interaction { ItemId = "i1", ActivityItemId = "act-1", ProviderName = "provider-1", ProviderInteractionId = "call-1", AgentId = "a1", Status = InteractionStatus.Ringing };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByActivityIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        interactionManager
            .Setup(m => m.UpdateAsync(
                interaction,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("interaction-update"))
            .Returns(ValueTask.CompletedTask);
        var activityManager = new Mock<IOmnichannelActivityManager>();
        var activity = new OmnichannelActivity
        {
            ItemId = "act-1",
            AssignmentStatus = ActivityAssignmentStatus.Reserved,
            Status = ActivityStatus.Pending,
            ReservationId = "r1",
            ReservedById = "u1",
            ReservedByUsername = "agent",
        };
        activityManager.Setup(m => m.FindByIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(activity);
        activityManager
            .Setup(m => m.UpdateAsync(
                activity,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("activity-update"))
            .Returns(ValueTask.CompletedTask);
        var publisher = new Mock<IContactCenterEventPublisher>();
        publisher
            .Setup(m => m.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("publish"))
            .Returns(Task.CompletedTask);
        var providerCommandStateService = new Mock<IProviderCommandStateService>(MockBehavior.Strict);
        ProviderCommandRegistration? capturedRegistration = null;
        providerCommandStateService
            .Setup(m => m.RegisterAsync(It.IsAny<ProviderCommandRegistration>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderCommandRegistration, CancellationToken>((registration, _) =>
            {
                capturedRegistration = registration;
                order.Add("register");
            })
            .ReturnsAsync(new ProviderCommand());
        Func<IProviderCommandProcessor, Task>? scheduledDispatch = null;
        var scopeExecutor = new Mock<IContactCenterScopeExecutor>(MockBehavior.Strict);
        scopeExecutor
            .Setup(m => m.ScheduleAfterCommit<IProviderCommandProcessor>(It.IsAny<Func<IProviderCommandProcessor, Task>>()))
            .Callback<Func<IProviderCommandProcessor, Task>>(operation =>
            {
                scheduledDispatch = operation;
                order.Add("schedule");
            })
            .Returns(true);
        var service = CreateService(
            reservationManager,
            queueItemManager,
            agentManager,
            queueManager,
            queueService,
            interactionManager,
            activityManager,
            publisher,
            providerCommandStateService,
            scopeExecutor);

        // Act
        await service.ExpireDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                "reservation-update",
                "queue-dequeue",
                "agent-update",
                "activity-update",
                "interaction-update",
                "publish",
                "register",
                "schedule",
            ],
            order);
        Assert.Equal(ReservationStatus.Expired, reservation.Status);
        Assert.Equal(QueueItemStatus.Removed, queueItem.Status);
        Assert.Null(queueItem.ReservationId);
        Assert.Null(queueItem.AgentId);
        Assert.Equal(_now, queueItem.DequeuedUtc);
        Assert.Equal(ActivityAssignmentStatus.Released, activity.AssignmentStatus);
        Assert.Equal(ActivityStatus.Completed, activity.Status);
        Assert.Null(activity.ReservationId);
        Assert.Null(activity.ReservedById);
        Assert.Null(activity.ReservedByUsername);
        Assert.Null(activity.ReservedUtc);
        Assert.Null(activity.ReservationExpiresUtc);
        Assert.Equal(_now, activity.CompletedUtc);
        Assert.Equal(InteractionStatus.Ringing, interaction.Status);
        Assert.Null(interaction.EndedUtc);
        Assert.Equal("Voicemail", interaction.TechnicalMetadata["unansweredOfferAction"]);
        Assert.Equal(AgentPresenceStatus.Available, agent.PresenceStatus);
        Assert.Null(agent.RequestedPresenceStatus);
        Assert.Null(agent.ActiveReservationId);
        Assert.NotNull(capturedRegistration);
        Assert.Equal(
            capturedRegistration!.CommandId,
            Assert.IsType<string>(interaction.TechnicalMetadata[ContactCenterConstants.CommandMetadata.CommandId]));
        AssertCallActionRegistration(
            capturedRegistration!,
            ProviderCommandType.SendToVoicemail,
            interaction,
            reservation,
            queue,
            agent,
            "call-1");
        await AssertScheduledDispatchAsync(scheduledDispatch, capturedRegistration!.CommandId);
        queueService.Verify(
            m => m.DequeueAsync(It.Is<QueueItem>(item => item.ItemId == "qi-1"), QueueItemStatus.Removed, It.IsAny<CancellationToken>()),
            Times.Once);
        providerCommandStateService.Verify(
            m => m.RegisterAsync(It.IsAny<ProviderCommandRegistration>(), It.IsAny<CancellationToken>()),
            Times.Once);
        scopeExecutor.Verify(
            m => m.ScheduleAfterCommit<IProviderCommandProcessor>(It.IsAny<Func<IProviderCommandProcessor, Task>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExpireDueAsync_WhenQueueUsesReject_RegistersRejectActionAndSchedulesDispatch()
    {
        // Arrange
        var order = new List<string>();
        var reservation = new ActivityReservation { ItemId = "r1", QueueId = "q1", QueueItemId = "qi-1", AgentId = "a1", ActivityItemId = "act-1", Status = ReservationStatus.Pending };
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.ListExpiredAsync(_now, It.IsAny<CancellationToken>())).ReturnsAsync([reservation]);
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
        reservationManager
            .Setup(m => m.UpdateAsync(
                reservation,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("reservation-update"))
            .Returns(ValueTask.CompletedTask);
        var queueItem = new QueueItem { ItemId = "qi-1", QueueId = "q1", Status = QueueItemStatus.Reserved, ReservationId = "r1", AgentId = "a1" };
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.FindByIdAsync("qi-1", It.IsAny<CancellationToken>())).ReturnsAsync(queueItem);
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1", UserName = "agent", DisplayName = "Agent", QueueIds = ["q1"] };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        agentManager
            .Setup(m => m.UpdateAsync(
                agent,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("agent-update"))
            .Returns(ValueTask.CompletedTask);
        var queue = new ActivityQueue { ItemId = "q1", Name = "Voice", UnansweredOfferAction = UnansweredOfferAction.Reject };
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager.Setup(m => m.FindByIdAsync("q1", It.IsAny<CancellationToken>())).ReturnsAsync(queue);
        var queueService = new Mock<IActivityQueueService>();
        queueService
            .Setup(m => m.DequeueAsync(queueItem, QueueItemStatus.Removed, It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                order.Add("queue-dequeue");
                queueItem.Status = QueueItemStatus.Removed;
            })
            .Returns(Task.CompletedTask);
        var interaction = new Interaction { ItemId = "i1", ActivityItemId = "act-1", ProviderName = "provider-1", ProviderInteractionId = "call-1", AgentId = "a1", Status = InteractionStatus.Ringing };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByActivityIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        interactionManager
            .Setup(m => m.UpdateAsync(
                interaction,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("interaction-update"))
            .Returns(ValueTask.CompletedTask);
        var activityManager = new Mock<IOmnichannelActivityManager>();
        var activity = new OmnichannelActivity
        {
            ItemId = "act-1",
            AssignmentStatus = ActivityAssignmentStatus.Reserved,
            Status = ActivityStatus.Pending,
            ReservationId = "r1",
            ReservedById = "u1",
            ReservedByUsername = "agent",
        };
        activityManager.Setup(m => m.FindByIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(activity);
        activityManager
            .Setup(m => m.UpdateAsync(
                activity,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("activity-update"))
            .Returns(ValueTask.CompletedTask);
        var publisher = new Mock<IContactCenterEventPublisher>();
        publisher
            .Setup(m => m.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("publish"))
            .Returns(Task.CompletedTask);
        var providerCommandStateService = new Mock<IProviderCommandStateService>(MockBehavior.Strict);
        ProviderCommandRegistration? capturedRegistration = null;
        providerCommandStateService
            .Setup(m => m.RegisterAsync(It.IsAny<ProviderCommandRegistration>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderCommandRegistration, CancellationToken>((registration, _) =>
            {
                capturedRegistration = registration;
                order.Add("register");
            })
            .ReturnsAsync(new ProviderCommand());
        Func<IProviderCommandProcessor, Task>? scheduledDispatch = null;
        var scopeExecutor = new Mock<IContactCenterScopeExecutor>(MockBehavior.Strict);
        scopeExecutor
            .Setup(m => m.ScheduleAfterCommit<IProviderCommandProcessor>(It.IsAny<Func<IProviderCommandProcessor, Task>>()))
            .Callback<Func<IProviderCommandProcessor, Task>>(operation =>
            {
                scheduledDispatch = operation;
                order.Add("schedule");
            })
            .Returns(true);
        var service = CreateService(
            reservationManager,
            queueItemManager,
            agentManager,
            queueManager,
            queueService,
            interactionManager,
            activityManager,
            publisher,
            providerCommandStateService,
            scopeExecutor);

        // Act
        await service.ExpireDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                "reservation-update",
                "queue-dequeue",
                "agent-update",
                "activity-update",
                "interaction-update",
                "publish",
                "register",
                "schedule",
            ],
            order);
        Assert.Equal(ReservationStatus.Expired, reservation.Status);
        Assert.Equal(QueueItemStatus.Removed, queueItem.Status);
        Assert.Null(queueItem.ReservationId);
        Assert.Null(queueItem.AgentId);
        Assert.Equal(_now, queueItem.DequeuedUtc);
        Assert.Equal(ActivityAssignmentStatus.Released, activity.AssignmentStatus);
        Assert.Equal(ActivityStatus.Cancelled, activity.Status);
        Assert.Null(activity.ReservationId);
        Assert.Null(activity.ReservedById);
        Assert.Null(activity.ReservedByUsername);
        Assert.Null(activity.ReservedUtc);
        Assert.Null(activity.ReservationExpiresUtc);
        Assert.Equal(_now, activity.CompletedUtc);
        Assert.Equal(InteractionStatus.Ringing, interaction.Status);
        Assert.Null(interaction.EndedUtc);
        Assert.Equal("Reject", interaction.TechnicalMetadata["unansweredOfferAction"]);
        Assert.Equal(AgentPresenceStatus.Available, agent.PresenceStatus);
        Assert.Null(agent.RequestedPresenceStatus);
        Assert.Null(agent.ActiveReservationId);
        Assert.NotNull(capturedRegistration);
        AssertCallActionRegistration(
            capturedRegistration!,
            ProviderCommandType.Reject,
            interaction,
            reservation,
            queue,
            agent,
            "call-1");
        await AssertScheduledDispatchAsync(scheduledDispatch, capturedRegistration!.CommandId);
        queueService.Verify(
            m => m.DequeueAsync(It.Is<QueueItem>(item => item.ItemId == "qi-1"), QueueItemStatus.Removed, It.IsAny<CancellationToken>()),
            Times.Once);
        providerCommandStateService.Verify(
            m => m.RegisterAsync(It.IsAny<ProviderCommandRegistration>(), It.IsAny<CancellationToken>()),
            Times.Once);
        scopeExecutor.Verify(
            m => m.ScheduleAfterCommit<IProviderCommandProcessor>(It.IsAny<Func<IProviderCommandProcessor, Task>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExpireDueAsync_WhenQueueUsesVoicemail_ButProviderCommandInfrastructureIsUnavailable_RequeuesInstead()
    {
        // Arrange
        var reservation = new ActivityReservation { ItemId = "r1", QueueId = "q1", QueueItemId = "qi-1", AgentId = "a1", ActivityItemId = "act-1", Status = ReservationStatus.Pending };
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.ListExpiredAsync(_now, It.IsAny<CancellationToken>())).ReturnsAsync([reservation]);
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
        var queueItem = new QueueItem { ItemId = "qi-1", QueueId = "q1", Status = QueueItemStatus.Reserved, ReservationId = "r1", AgentId = "a1" };
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.FindByIdAsync("qi-1", It.IsAny<CancellationToken>())).ReturnsAsync(queueItem);
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1", UserName = "agent", DisplayName = "Agent", QueueIds = ["q1"] };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager.Setup(m => m.FindByIdAsync("q1", It.IsAny<CancellationToken>())).ReturnsAsync(new ActivityQueue { ItemId = "q1", Name = "Voice", UnansweredOfferAction = UnansweredOfferAction.Voicemail });
        var queueService = new Mock<IActivityQueueService>();
        var activity = new OmnichannelActivity
        {
            ItemId = "act-1",
            AssignmentStatus = ActivityAssignmentStatus.Reserved,
            Status = ActivityStatus.Pending,
            ReservationId = "r1",
            ReservedById = "u1",
            ReservedByUsername = "agent",
        };
        var interaction = new Interaction { ItemId = "i1", ActivityItemId = "act-1", ProviderName = "provider-1", ProviderInteractionId = "call-1", AgentId = "a1", Status = InteractionStatus.Ringing };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByActivityIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.FindByIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(activity);
        var publisher = new Mock<IContactCenterEventPublisher>();
        var scopeExecutor = new Mock<IContactCenterScopeExecutor>(MockBehavior.Strict);
        var service = CreateService(
            reservationManager,
            queueItemManager,
            agentManager,
            queueManager,
            queueService,
            interactionManager,
            activityManager,
            publisher,
            providerCommandStateService: null,
            scopeExecutor: scopeExecutor);

        // Act
        await service.ExpireDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReservationStatus.Expired, reservation.Status);
        Assert.Equal(QueueItemStatus.Waiting, queueItem.Status);
        Assert.Null(queueItem.ReservationId);
        Assert.Null(queueItem.AgentId);
        Assert.Null(queueItem.DequeuedUtc);
        Assert.Equal(AgentPresenceStatus.Available, agent.PresenceStatus);
        Assert.Null(agent.RequestedPresenceStatus);
        Assert.Null(agent.ActiveReservationId);
        Assert.Equal(ActivityAssignmentStatus.Available, activity.AssignmentStatus);
        Assert.Equal(ActivityStatus.Pending, activity.Status);
        Assert.Null(activity.ReservationId);
        Assert.Null(activity.ReservedById);
        Assert.Null(activity.ReservedByUsername);
        Assert.Null(activity.ReservedUtc);
        Assert.Null(activity.ReservationExpiresUtc);
        Assert.Null(activity.CompletedUtc);
        Assert.Equal(InteractionStatus.Created, interaction.Status);
        Assert.Null(interaction.AgentId);
        Assert.Null(interaction.EndedUtc);
        Assert.False(interaction.TechnicalMetadata.ContainsKey(ContactCenterConstants.CommandMetadata.CommandId));
        queueService.Verify(
            m => m.DequeueAsync(It.IsAny<QueueItem>(), It.IsAny<QueueItemStatus>(), It.IsAny<CancellationToken>()),
            Times.Never);
        scopeExecutor.Verify(
            m => m.ScheduleAfterCommit<IProviderCommandProcessor>(It.IsAny<Func<IProviderCommandProcessor, Task>>()),
            Times.Never);
    }

    [Fact]
    public async Task ExpireDueAsync_WhenQueueUsesReject_ButCallIdentityIsUnavailable_RequeuesInstead()
    {
        // Arrange
        var reservation = new ActivityReservation { ItemId = "r1", QueueId = "q1", QueueItemId = "qi-1", AgentId = "a1", ActivityItemId = "act-1", Status = ReservationStatus.Pending };
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.ListExpiredAsync(_now, It.IsAny<CancellationToken>())).ReturnsAsync([reservation]);
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
        var queueItem = new QueueItem { ItemId = "qi-1", QueueId = "q1", Status = QueueItemStatus.Reserved, ReservationId = "r1", AgentId = "a1" };
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.FindByIdAsync("qi-1", It.IsAny<CancellationToken>())).ReturnsAsync(queueItem);
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1", UserName = "agent", DisplayName = "Agent", QueueIds = ["q1"] };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager.Setup(m => m.FindByIdAsync("q1", It.IsAny<CancellationToken>())).ReturnsAsync(new ActivityQueue { ItemId = "q1", Name = "Voice", UnansweredOfferAction = UnansweredOfferAction.Reject });
        var queueService = new Mock<IActivityQueueService>();
        var activity = new OmnichannelActivity
        {
            ItemId = "act-1",
            AssignmentStatus = ActivityAssignmentStatus.Reserved,
            Status = ActivityStatus.Pending,
            ReservationId = "r1",
            ReservedById = "u1",
            ReservedByUsername = "agent",
        };
        var interaction = new Interaction { ItemId = "i1", ActivityItemId = "act-1", ProviderName = "provider-1", Status = InteractionStatus.Ringing };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByActivityIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.FindByIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(activity);
        var publisher = new Mock<IContactCenterEventPublisher>();
        var providerCommandStateService = new Mock<IProviderCommandStateService>(MockBehavior.Strict);
        var scopeExecutor = new Mock<IContactCenterScopeExecutor>(MockBehavior.Strict);
        var service = CreateService(
            reservationManager,
            queueItemManager,
            agentManager,
            queueManager,
            queueService,
            interactionManager,
            activityManager,
            publisher,
            providerCommandStateService,
            scopeExecutor);

        // Act
        await service.ExpireDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReservationStatus.Expired, reservation.Status);
        Assert.Equal(QueueItemStatus.Waiting, queueItem.Status);
        Assert.Null(queueItem.ReservationId);
        Assert.Null(queueItem.AgentId);
        Assert.Null(queueItem.DequeuedUtc);
        Assert.Equal(AgentPresenceStatus.Available, agent.PresenceStatus);
        Assert.Null(agent.RequestedPresenceStatus);
        Assert.Null(agent.ActiveReservationId);
        Assert.Equal(ActivityAssignmentStatus.Available, activity.AssignmentStatus);
        Assert.Equal(ActivityStatus.Pending, activity.Status);
        Assert.Null(activity.ReservationId);
        Assert.Null(activity.ReservedById);
        Assert.Null(activity.ReservedByUsername);
        Assert.Null(activity.ReservedUtc);
        Assert.Null(activity.ReservationExpiresUtc);
        Assert.Null(activity.CompletedUtc);
        Assert.Equal(InteractionStatus.Created, interaction.Status);
        Assert.Null(interaction.AgentId);
        Assert.Null(interaction.EndedUtc);
        Assert.False(interaction.TechnicalMetadata.ContainsKey(ContactCenterConstants.CommandMetadata.CommandId));
        providerCommandStateService.Verify(
            m => m.RegisterAsync(It.IsAny<ProviderCommandRegistration>(), It.IsAny<CancellationToken>()),
            Times.Never);
        queueService.Verify(
            m => m.DequeueAsync(It.IsAny<QueueItem>(), It.IsAny<QueueItemStatus>(), It.IsAny<CancellationToken>()),
            Times.Never);
        scopeExecutor.Verify(
            m => m.ScheduleAfterCommit<IProviderCommandProcessor>(It.IsAny<Func<IProviderCommandProcessor, Task>>()),
            Times.Never);
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
        return CreateService(
            reservationManager,
            queueItemManager,
            agentManager,
            queueManager,
            queueService,
            interactionManager,
            activityManager,
            publisher,
            providerCommandStateService: null,
            scopeExecutor: null,
            distributedLock: distributedLock,
            session: session,
            availabilityService: availabilityService);
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
        Mock<IProviderCommandStateService>? providerCommandStateService,
        Mock<IContactCenterScopeExecutor>? scopeExecutor,
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
            providerCommandStateService is null ? [] : [providerCommandStateService.Object],
            (scopeExecutor ?? new Mock<IContactCenterScopeExecutor>(MockBehavior.Strict)).Object,
            distributedLock.Object,
            session.Object,
            clock.Object,
            new Mock<ILogger<ActivityReservationService>>().Object);
    }

    private static void AssertCallActionRegistration(
        ProviderCommandRegistration registration,
        ProviderCommandType expectedCommandType,
        Interaction interaction,
        ActivityReservation reservation,
        ActivityQueue queue,
        AgentProfile agent,
        string providerCallId)
    {
        Assert.NotNull(registration);
        Assert.Equal(interaction.ProviderName, registration.ProviderName);
        Assert.Equal(expectedCommandType, registration.CommandType);
        Assert.Equal(reservation.ActivityItemId, registration.ActivityItemId);
        Assert.Equal(interaction.ItemId, registration.InteractionId);
        Assert.False(string.IsNullOrWhiteSpace(registration.CommandId));
        Assert.False(string.IsNullOrWhiteSpace(registration.RequestPayload));
        Assert.Equal(registration.CommandId, Assert.IsType<string>(interaction.TechnicalMetadata[ContactCenterConstants.CommandMetadata.CommandId]));

        using var request = JsonDocument.Parse(registration.RequestPayload);
        Assert.Equal(reservation.ActivityItemId, request.RootElement.GetProperty("ActivityItemId").GetString());
        Assert.Equal(reservation.QueueId, request.RootElement.GetProperty("QueueId").GetString());
        Assert.Equal(providerCallId, request.RootElement.GetProperty("ProviderCallId").GetString());
        Assert.True(request.RootElement.GetProperty("ReofferOnFailure").GetBoolean());
        var metadata = request.RootElement.GetProperty("Metadata");
        Assert.Equal(queue.ItemId, metadata.GetProperty("queueId").GetString());
        Assert.Equal(queue.Name, metadata.GetProperty("queueName").GetString());
        Assert.Equal(agent.UserId, metadata.GetProperty("voicemailRecipientUserId").GetString());
        Assert.Equal(agent.UserName, metadata.GetProperty("voicemailRecipientUserName").GetString());
        Assert.Equal(agent.DisplayName, metadata.GetProperty("voicemailRecipientDisplayName").GetString());
    }

    private static async Task AssertScheduledDispatchAsync(Func<IProviderCommandProcessor, Task>? scheduledDispatch, string commandId)
    {
        Assert.NotNull(scheduledDispatch);
        var dispatch = scheduledDispatch!;

        var processor = new Mock<IProviderCommandProcessor>(MockBehavior.Strict);
        processor
            .Setup(value => value.DispatchAsync(commandId, CancellationToken.None))
            .ReturnsAsync(new ProviderCommand { CommandId = commandId, Status = ProviderCommandStatus.Sent });

        await dispatch(processor.Object);

        processor.Verify(
            value => value.DispatchAsync(commandId, CancellationToken.None),
            Times.Once);
    }
}
