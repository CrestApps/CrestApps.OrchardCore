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
    public async Task HealForAvailabilityAsync_WhenDialerReservationHasNoInteraction_PreservesIt()
    {
        // Arrange
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.SetupSequence(manager => manager.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                PresenceStatus = AgentPresenceStatus.Available,
                ActiveReservationId = "r1",
            })
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                PresenceStatus = AgentPresenceStatus.Reserved,
                ActiveReservationId = "r1",
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
            .ReturnsAsync((Interaction)null);
        interactionManager.Setup(manager => manager.FindActiveByAgentAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Interaction)null);

        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(manager => manager.FindByIdAsync("act-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelActivity
            {
                ItemId = "act-1",
                Source = ActivitySources.PreviewDial,
                AssignmentStatus = ActivityAssignmentStatus.Reserved,
                ReservationId = "r1",
            });

        var service = CreateService(
            agentManager,
            reservationManager,
            reservationService,
            queueItemManager,
            interactionManager,
            activityManager);

        // Act
        var healed = await service.HealForAvailabilityAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, healed);
        reservationService.Verify(manager => manager.CancelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HealForAvailabilityAsync_WhenAvailableAgentHasProviderConfirmedConnectedInteraction_PreservesIt()
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
        interaction.ProviderName = "provider-1";
        interaction.ProviderInteractionId = "call-1";
        var synchronizationService = new Mock<IProviderCallStateSynchronizationService>();
        synchronizationService
            .Setup(service => service.RefreshInteractionAsync(interaction, It.IsAny<CancellationToken>()))
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

        var service = CreateService(
            agentManager,
            reservationManager,
            reservationService,
            queueItemManager,
            interactionManager,
            activityManager,
            synchronizationService);

        // Act
        var healed = await service.HealForAvailabilityAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, healed);
        Assert.Equal(QueueItemStatus.Assigned, queueItem.Status);
        Assert.Equal("a1", queueItem.AgentId);
        Assert.Equal("r1", queueItem.ReservationId);
        Assert.Equal(InteractionStatus.Connected, interaction.Status);
        Assert.Equal("a1", interaction.AgentId);
        Assert.Equal(ActivityAssignmentStatus.Assigned, activity.AssignmentStatus);
        Assert.Equal("u1", activity.AssignedToId);
        Assert.Equal("r1", activity.ReservationId);
    }

    [Fact]
    public async Task HealForAvailabilityAsync_WhenConnectedInteractionHasNoProvider_RequeuesIt()
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
        var activity = new OmnichannelActivity
        {
            ItemId = "act-1",
            AssignmentStatus = ActivityAssignmentStatus.Assigned,
            AssignedToId = "u1",
            ReservationId = "r1",
        };
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(manager => manager.FindByIdAsync("act-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(activity);
        var service = CreateService(
            agentManager,
            reservationManager,
            new Mock<IActivityReservationService>(),
            queueItemManager,
            interactionManager,
            activityManager);

        // Act
        var healed = await service.HealForAvailabilityAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, healed);
        Assert.Equal(QueueItemStatus.Waiting, queueItem.Status);
        Assert.Null(queueItem.AgentId);
        Assert.Equal(InteractionStatus.Created, interaction.Status);
        Assert.Null(interaction.AgentId);
        Assert.Equal(ActivityAssignmentStatus.Available, activity.AssignmentStatus);
        Assert.Null(activity.AssignedToId);
    }

    [Fact]
    public async Task HealForAvailabilityAsync_WhenRingingInteractionHasNoActiveReservation_RequeuesIt()
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
            Status = QueueItemStatus.Reserved,
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
            Status = InteractionStatus.Ringing,
        };

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(manager => manager.FindActiveByAgentAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var activityManager = new Mock<IOmnichannelActivityManager>();
        var activity = new OmnichannelActivity
        {
            ItemId = "act-1",
            AssignmentStatus = ActivityAssignmentStatus.Reserved,
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

    [Fact]
    public async Task HealForAvailabilityAsync_WhenRingingInteractionIsProviderBackedAndStillLive_DoesNotRequeue()
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

        var queueItem = new QueueItem
        {
            ItemId = "qi-1",
            ActivityItemId = "act-1",
            QueueId = "q1",
            ReservationId = "r1",
            AgentId = "a1",
            Status = QueueItemStatus.Reserved,
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
            Status = InteractionStatus.Ringing,
            ProviderName = "provider-1",
            ProviderInteractionId = "call-1",
        };

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(manager => manager.FindActiveByAgentAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var synchronizationService = new Mock<IProviderCallStateSynchronizationService>();
        synchronizationService
            .Setup(service => service.RefreshInteractionAsync(interaction, It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var activityManager = new Mock<IOmnichannelActivityManager>();

        var service = CreateService(
            agentManager,
            reservationManager,
            new Mock<IActivityReservationService>(),
            queueItemManager,
            interactionManager,
            activityManager,
            synchronizationService);

        // Act
        var healed = await service.HealForAvailabilityAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, healed);
        Assert.Equal(QueueItemStatus.Reserved, queueItem.Status);
        Assert.Equal("a1", queueItem.AgentId);
        Assert.Equal(InteractionStatus.Ringing, interaction.Status);
        Assert.Equal("a1", interaction.AgentId);
    }

    private static AgentWorkStateHealingService CreateService(
        Mock<IAgentProfileManager> agentManager,
        Mock<IActivityReservationManager> reservationManager,
        Mock<IActivityReservationService> reservationService,
        Mock<IQueueItemManager> queueItemManager,
        Mock<IInteractionManager> interactionManager,
        Mock<IOmnichannelActivityManager> activityManager = null,
        Mock<IProviderCallStateSynchronizationService> synchronizationService = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(provider => provider.GetService(typeof(IProviderCallStateSynchronizationService)))
            .Returns(synchronizationService?.Object ?? Mock.Of<IProviderCallStateSynchronizationService>());

        return new AgentWorkStateHealingService(
            agentManager.Object,
            reservationManager.Object,
            reservationService.Object,
            queueItemManager.Object,
            interactionManager.Object,
            activityManager?.Object ?? Mock.Of<IOmnichannelActivityManager>(),
            serviceProvider.Object,
            clock.Object,
            NullLogger<AgentWorkStateHealingService>.Instance);
    }
}
