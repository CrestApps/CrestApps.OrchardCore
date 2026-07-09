using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class AgentWorkStateHealingServiceTests
{
    private static readonly DateTime _now = new(2026, 7, 9, 17, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HealForAvailabilityAsync_WhenPendingReservationIsStale_CancelsIt()
    {
        // Arrange
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.SetupSequence(manager => manager.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                PresenceStatus = AgentPresenceStatus.Available,
            })
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                PresenceStatus = AgentPresenceStatus.Available,
            });

        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(manager => manager.FindPendingByAgentAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityReservation
            {
                ItemId = "r1",
                ActivityItemId = "act-1",
                QueueItemId = "qi-1",
                AgentId = "a1",
                Status = ReservationStatus.Pending,
                ExpiresUtc = _now.AddSeconds(30),
            });

        var reservationService = new Mock<IActivityReservationService>();
        reservationService.Setup(service => service.CancelAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityReservation { ItemId = "r1" });

        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(manager => manager.FindByIdAsync("qi-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueItem
            {
                ItemId = "qi-1",
                ReservationId = "r1",
                AgentId = "a1",
                Status = QueueItemStatus.Reserved,
            });

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(manager => manager.FindByActivityIdAsync("act-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction
            {
                ItemId = "i1",
                ActivityItemId = "act-1",
                AgentId = null,
                Status = InteractionStatus.Created,
            });

        var service = CreateService(agentManager, reservationManager, reservationService, queueItemManager, interactionManager);

        // Act
        var healed = await service.HealForAvailabilityAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, healed);
        reservationService.Verify(manager => manager.CancelAsync("r1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HealForAvailabilityAsync_WhenAvailableAgentHasConnectedInteraction_RequeuesIt()
    {
        // Arrange
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.SetupSequence(manager => manager.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                PresenceStatus = AgentPresenceStatus.Available,
            })
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                PresenceStatus = AgentPresenceStatus.Available,
            });

        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(manager => manager.FindPendingByAgentAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActivityReservation)null);

        var reservationService = new Mock<IActivityReservationService>();
        var queueItem = new QueueItem
        {
            ItemId = "qi-1",
            ActivityItemId = "act-1",
            QueueId = "q1",
            ReservationId = "r1",
            AgentId = "a1",
            Status = QueueItemStatus.Assigned,
        };

        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(manager => manager.FindByActivityIdAsync("act-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueItem);

        var interaction = new Interaction
        {
            ItemId = "i1",
            ActivityItemId = "act-1",
            QueueId = "q1",
            AgentId = "a1",
            Status = InteractionStatus.Connected,
        };

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(manager => manager.FindActiveByAgentAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var activityManager = new Mock<IOmnichannelActivityManager>();
        var activity = new OmnichannelActivity
        {
            ItemId = "act-1",
            AssignmentStatus = ActivityAssignmentStatus.Assigned,
            AssignedToId = "u1",
            ReservationId = "r1",
        };
        activityManager.Setup(manager => manager.FindByIdAsync("act-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(activity);

        var service = CreateService(agentManager, reservationManager, reservationService, queueItemManager, interactionManager, activityManager);

        // Act
        var healed = await service.HealForAvailabilityAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, healed);
        Assert.Equal(QueueItemStatus.Waiting, queueItem.Status);
        Assert.Null(queueItem.AgentId);
        Assert.Null(queueItem.ReservationId);
        Assert.Equal(InteractionStatus.Created, interaction.Status);
        Assert.Null(interaction.AgentId);
        Assert.Equal(ActivityAssignmentStatus.Available, activity.AssignmentStatus);
        Assert.Null(activity.AssignedToId);
        Assert.Null(activity.ReservationId);
    }

    [Fact]
    public async Task HealForResetAsync_WhenPendingReservationExists_CancelsItEvenWhenOtherwiseValid()
    {
        // Arrange
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.SetupSequence(manager => manager.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                PresenceStatus = AgentPresenceStatus.Busy,
                ActiveReservationId = "r1",
            })
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                PresenceStatus = AgentPresenceStatus.Busy,
            });

        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(manager => manager.FindByIdAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityReservation
            {
                ItemId = "r1",
                ActivityItemId = "act-1",
                QueueItemId = "qi-1",
                AgentId = "a1",
                Status = ReservationStatus.Pending,
                ExpiresUtc = _now.AddSeconds(30),
            });

        var reservationService = new Mock<IActivityReservationService>();
        reservationService.Setup(service => service.CancelAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityReservation { ItemId = "r1" });

        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(manager => manager.FindByIdAsync("qi-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueItem
            {
                ItemId = "qi-1",
                ReservationId = "r1",
                AgentId = "a1",
                Status = QueueItemStatus.Reserved,
            });

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(manager => manager.FindByActivityIdAsync("act-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction
            {
                ItemId = "i1",
                ActivityItemId = "act-1",
                AgentId = "a1",
                Status = InteractionStatus.Ringing,
            });

        var service = CreateService(agentManager, reservationManager, reservationService, queueItemManager, interactionManager);

        // Act
        var healed = await service.HealForResetAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, healed);
        reservationService.Verify(manager => manager.CancelAsync("r1", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static AgentWorkStateHealingService CreateService(
        Mock<IAgentProfileManager> agentManager,
        Mock<IActivityReservationManager> reservationManager,
        Mock<IActivityReservationService> reservationService,
        Mock<IQueueItemManager> queueItemManager,
        Mock<IInteractionManager> interactionManager,
        Mock<IOmnichannelActivityManager> activityManager = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new AgentWorkStateHealingService(
            agentManager.Object,
            reservationManager.Object,
            reservationService.Object,
            queueItemManager.Object,
            interactionManager.Object,
            activityManager?.Object ?? Mock.Of<IOmnichannelActivityManager>(),
            clock.Object,
            NullLogger<AgentWorkStateHealingService>.Instance);
    }
}
