using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ActivityAssignmentServiceTests
{
    [Fact]
    public async Task AssignNextAsync_WhenNoWaitingItems_ReturnsNull()
    {
        // Arrange
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(m => m.ListWaitingAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = CreateService(queueItemManager, new Mock<IAgentProfileManager>(), new Mock<IActivityReservationService>());

        // Act
        var reservation = await service.AssignNextAsync("q1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(reservation);
    }

    [Fact]
    public async Task AssignNextAsync_WhenNoAvailableAgents_ReturnsNull()
    {
        // Arrange
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(m => m.ListWaitingAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new QueueItem { ItemId = "i1", QueueId = "q1" }]);

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(m => m.ListAvailableForQueueAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = CreateService(queueItemManager, agentManager, new Mock<IActivityReservationService>());

        // Act
        var reservation = await service.AssignNextAsync("q1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(reservation);
    }

    [Fact]
    public async Task AssignNextAsync_ReservesTopItemForLongestIdleAgent()
    {
        // Arrange
        var topItem = new QueueItem { ItemId = "i1", QueueId = "q1" };
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(m => m.ListWaitingAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([topItem]);

        var idleAgent = new AgentProfile { ItemId = "a1", PresenceChangedUtc = new DateTime(2026, 1, 1) };
        var busyAgent = new AgentProfile { ItemId = "a2", PresenceChangedUtc = new DateTime(2026, 1, 2) };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(m => m.ListAvailableForQueueAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([busyAgent, idleAgent]);

        var queueManager = new Mock<IActivityQueueManager>();
        queueManager
            .Setup(m => m.FindByIdAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityQueue { ItemId = "q1", ReservationTimeoutSeconds = 45 });

        var reservationService = new Mock<IActivityReservationService>();
        reservationService
            .Setup(s => s.ReserveAsync(topItem, idleAgent, 45, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityReservation { ItemId = "r1" });

        var service = CreateService(queueItemManager, agentManager, queueManager, reservationService);

        // Act
        var reservation = await service.AssignNextAsync("q1", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(reservation);
        reservationService.Verify(s => s.ReserveAsync(topItem, idleAgent, 45, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignNextAsync_WhenQueueRequiresSkill_SelectsSkilledAgent()
    {
        // Arrange
        var topItem = new QueueItem { ItemId = "i1", QueueId = "q1" };
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(m => m.ListWaitingAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([topItem]);

        var missingSkillAgent = new AgentProfile
        {
            ItemId = "a1",
            Skills = ["general"],
            PresenceChangedUtc = new DateTime(2026, 1, 1),
        };

        var skilledAgent = new AgentProfile
        {
            ItemId = "a2",
            Skills = ["billing"],
            PresenceChangedUtc = new DateTime(2026, 1, 2),
        };

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(m => m.ListAvailableForQueueAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([missingSkillAgent, skilledAgent]);

        var queueManager = new Mock<IActivityQueueManager>();
        queueManager
            .Setup(m => m.FindByIdAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityQueue
            {
                ItemId = "q1",
                ReservationTimeoutSeconds = 45,
                RequiredSkills = ["billing"],
                Enabled = true,
            });

        var reservationService = new Mock<IActivityReservationService>();
        reservationService
            .Setup(s => s.ReserveAsync(topItem, skilledAgent, 45, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityReservation { ItemId = "r1" });

        var service = CreateService(queueItemManager, agentManager, queueManager, reservationService);

        // Act
        var reservation = await service.AssignNextAsync("q1", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(reservation);
        reservationService.Verify(s => s.ReserveAsync(topItem, skilledAgent, 45, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignNextAsync_WhenLockNotAcquired_ReturnsNullWithoutReserving()
    {
        // Arrange
        var topItem = new QueueItem { ItemId = "i1", QueueId = "q1" };
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(m => m.ListWaitingAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([topItem]);

        var reservationService = new Mock<IActivityReservationService>();

        var service = CreateService(
            queueItemManager,
            new Mock<IAgentProfileManager>(),
            new Mock<IActivityQueueManager>(),
            reservationService,
            CreateDistributedLock(locked: false));

        // Act
        var reservation = await service.AssignNextAsync("q1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(reservation);
        queueItemManager.Verify(m => m.ListWaitingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        reservationService.Verify(
            s => s.ReserveAsync(It.IsAny<QueueItem>(), It.IsAny<AgentProfile>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ActivityAssignmentService CreateService(
        Mock<IQueueItemManager> queueItemManager,
        Mock<IAgentProfileManager> agentManager,
        Mock<IActivityReservationService> reservationService)
    {
        var queueManager = new Mock<IActivityQueueManager>();

        return CreateService(queueItemManager, agentManager, queueManager, reservationService);
    }

    private static ActivityAssignmentService CreateService(
        Mock<IQueueItemManager> queueItemManager,
        Mock<IAgentProfileManager> agentManager,
        Mock<IActivityQueueManager> queueManager,
        Mock<IActivityReservationService> reservationService)
    {
        return CreateService(queueItemManager, agentManager, queueManager, reservationService, CreateDistributedLock(locked: true));
    }

    private static ActivityAssignmentService CreateService(
        Mock<IQueueItemManager> queueItemManager,
        Mock<IAgentProfileManager> agentManager,
        Mock<IActivityQueueManager> queueManager,
        Mock<IActivityReservationService> reservationService,
        Mock<IDistributedLock> distributedLock)
    {
        var businessHours = new Mock<IBusinessHoursService>();
        businessHours
            .Setup(b => b.IsOpenAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        return new ActivityAssignmentService(
            queueItemManager.Object,
            agentManager.Object,
            queueManager.Object,
            CreateRoutingService(),
            reservationService.Object,
            businessHours.Object,
            new Mock<IContactCenterEventPublisher>().Object,
            distributedLock.Object,
            clock.Object);
    }

    private static Mock<IDistributedLock> CreateDistributedLock(bool locked)
    {
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((null, locked));

        return distributedLock;
    }

    private static ActivityRoutingService CreateRoutingService()
    {
        return new ActivityRoutingService(
        [
            new RequiredSkillsRoutingStrategy(),
            new LongestIdleRoutingStrategy(),
        ]);
    }
}
