using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class AgentPresenceManagerServiceTests
{
    private static readonly DateTime _now = new(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SignInAsync_CreatesAvailableProfile_AndJoinsQueues()
    {
        // Arrange
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync((AgentProfile)null);
        agentManager.Setup(m => m.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AgentProfile { ItemId = "a1" });
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync((AgentProfile)null);
        var publisher = new Mock<IContactCenterEventPublisher>();
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);
        var service = new AgentPresenceManagerService(agentManager.Object, [], [], publisher.Object, CreateDistributedLock().Object, clock.Object, new Mock<ILogger<AgentPresenceManagerService>>().Object);

        // Act
        var profile = await service.SignInAsync("u1", ["q1", "q2"], [], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.Available, profile.PresenceStatus);
        Assert.Null(profile.ActiveReservationId);
        Assert.Equal(2, profile.QueueIds.Count);
        agentManager.Verify(m => m.CreateAsync(It.IsAny<AgentProfile>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SignInAsync_PublishesAgentSignedInEvent_ForQueuedOfferHandlers()
    {
        // Arrange
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync((AgentProfile)null);
        agentManager.Setup(m => m.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AgentProfile { ItemId = "a1" });
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync((AgentProfile)null);
        var publisher = new Mock<IContactCenterEventPublisher>();
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);
        var service = new AgentPresenceManagerService(agentManager.Object, [], [], publisher.Object, CreateDistributedLock().Object, clock.Object, new Mock<ILogger<AgentPresenceManagerService>>().Object);

        // Act
        await service.SignInAsync("u1", ["q1"], [], TestContext.Current.CancellationToken);

        // Assert
        publisher.Verify(publisher => publisher.PublishAsync(
            It.Is<InteractionEvent>(interactionEvent =>
                interactionEvent.EventType == ContactCenterConstants.Events.AgentSignedIn &&
                interactionEvent.AggregateId == "a1" &&
                interactionEvent.ActorId == "u1"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SignOutAsync_ClearsMembershipAndSetsOffline()
    {
        // Arrange
        var existing = new AgentProfile
        {
            ItemId = "a1",
            UserId = "u1",
            PresenceStatus = AgentPresenceStatus.Available,
            PresenceReason = "Ready",
            QueueIds = ["q1"],
            CampaignIds = ["c1"],
        };

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);
        var service = new AgentPresenceManagerService(agentManager.Object, [], [], new Mock<IContactCenterEventPublisher>().Object, CreateDistributedLock().Object, clock.Object, new Mock<ILogger<AgentPresenceManagerService>>().Object);

        // Act
        var profile = await service.SignOutAsync("u1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.Offline, profile.PresenceStatus);
        Assert.Null(profile.PresenceReason);
        Assert.Empty(profile.QueueIds);
        Assert.Empty(profile.CampaignIds);
    }

    [Fact]
    public async Task SignOutAsync_WhenLiveSessionExists_ClearsSessionMembership()
    {
        // Arrange
        var existing = new AgentProfile
        {
            ItemId = "a1",
            UserId = "u1",
            PresenceStatus = AgentPresenceStatus.Available,
            QueueIds = ["q1"],
            CampaignIds = ["c1"],
        };
        var session = new AgentSession
        {
            ItemId = "s1",
            UserId = "u1",
            QueueIds = ["q1"],
            CampaignIds = ["c1"],
        };

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var sessionManager = new Mock<IAgentSessionManager>();
        sessionManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(session);

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);
        var service = new AgentPresenceManagerService(agentManager.Object, [sessionManager.Object], [], new Mock<IContactCenterEventPublisher>().Object, CreateDistributedLock().Object, clock.Object, new Mock<ILogger<AgentPresenceManagerService>>().Object);

        // Act
        await service.SignOutAsync("u1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(session.QueueIds);
        Assert.Empty(session.CampaignIds);
        Assert.Equal(_now, session.ModifiedUtc);
        sessionManager.Verify(m => m.UpdateAsync(session, It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetPresenceAsync_WhenProfileDoesNotExist_CreatesProfile()
    {
        // Arrange
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync((AgentProfile)null);
        agentManager.Setup(m => m.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AgentProfile { ItemId = "a1" });
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync((AgentProfile)null);
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);
        var service = new AgentPresenceManagerService(agentManager.Object, [], [], new Mock<IContactCenterEventPublisher>().Object, CreateDistributedLock().Object, clock.Object, new Mock<ILogger<AgentPresenceManagerService>>().Object);

        // Act
        var profile = await service.SetPresenceAsync("u1", AgentPresenceStatus.DoNotDisturb, "Focus time", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("u1", profile.UserId);
        Assert.Equal(AgentPresenceStatus.DoNotDisturb, profile.PresenceStatus);
        Assert.Equal("Focus time", profile.PresenceReason);
        agentManager.Verify(m => m.CreateAsync(It.IsAny<AgentProfile>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetPresenceAsync_RequestBreakWhenAvailable_GrantsBreakImmediately()
    {
        // Arrange
        var existing = new AgentProfile { ItemId = "a1", UserId = "u1", PresenceStatus = AgentPresenceStatus.Available };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);
        var service = new AgentPresenceManagerService(agentManager.Object, [], [], new Mock<IContactCenterEventPublisher>().Object, CreateDistributedLock().Object, clock.Object, new Mock<ILogger<AgentPresenceManagerService>>().Object);

        // Act
        var profile = await service.SetPresenceAsync("u1", AgentPresenceStatus.RequestBreak, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.Break, profile.PresenceStatus);
        Assert.Null(profile.RequestedPresenceStatus);
    }

    [Fact]
    public async Task SetPresenceAsync_RequestBreakWhenReserved_KeepsReservationAndStoresPendingBreak()
    {
        // Arrange
        var existing = new AgentProfile
        {
            ItemId = "a1",
            UserId = "u1",
            PresenceStatus = AgentPresenceStatus.Reserved,
            ActiveReservationId = "r1",
        };

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);
        var service = new AgentPresenceManagerService(agentManager.Object, [], [], new Mock<IContactCenterEventPublisher>().Object, CreateDistributedLock().Object, clock.Object, new Mock<ILogger<AgentPresenceManagerService>>().Object);

        // Act
        var profile = await service.SetPresenceAsync("u1", AgentPresenceStatus.RequestBreak, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.Reserved, profile.PresenceStatus);
        Assert.Equal(AgentPresenceStatus.Break, profile.RequestedPresenceStatus);
        Assert.Equal("r1", profile.ActiveReservationId);
    }

    [Fact]
    public async Task StartWrapUpAsync_MovesBusyAgentIntoWrapUp()
    {
        // Arrange
        var existing = new AgentProfile { ItemId = "a1", UserId = "u1", PresenceStatus = AgentPresenceStatus.Busy, ActiveReservationId = "r1" };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);
        var service = new AgentPresenceManagerService(agentManager.Object, [], [], new Mock<IContactCenterEventPublisher>().Object, CreateDistributedLock().Object, clock.Object, new Mock<ILogger<AgentPresenceManagerService>>().Object);

        // Act
        var profile = await service.StartWrapUpAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.WrapUp, profile.PresenceStatus);
        Assert.Null(profile.ActiveReservationId);
    }

    [Fact]
    public async Task CompleteWorkAsync_WhenBreakRequested_AppliesBreak()
    {
        // Arrange
        var existing = new AgentProfile
        {
            ItemId = "a1",
            UserId = "u1",
            PresenceStatus = AgentPresenceStatus.WrapUp,
            RequestedPresenceStatus = AgentPresenceStatus.Break,
            QueueIds = ["q1"],
        };

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);
        var service = new AgentPresenceManagerService(agentManager.Object, [], [], new Mock<IContactCenterEventPublisher>().Object, CreateDistributedLock().Object, clock.Object, new Mock<ILogger<AgentPresenceManagerService>>().Object);

        // Act
        var profile = await service.CompleteWorkAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.Break, profile.PresenceStatus);
        Assert.Null(profile.RequestedPresenceStatus);
    }

    [Fact]
    public async Task SignInAsync_WhenExistingProfilePresent_HealsStaleWorkBeforeResettingAvailability()
    {
        // Arrange
        var existing = new AgentProfile
        {
            ItemId = "a1",
            UserId = "u1",
            PresenceStatus = AgentPresenceStatus.Available,
            QueueIds = ["q1"],
        };

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var healer = new Mock<IAgentWorkStateHealingService>();
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);
        var service = new AgentPresenceManagerService(agentManager.Object, [], [healer.Object], new Mock<IContactCenterEventPublisher>().Object, CreateDistributedLock().Object, clock.Object, new Mock<ILogger<AgentPresenceManagerService>>().Object);

        // Act
        await service.SignInAsync("u1", ["q2"], [], TestContext.Current.CancellationToken);

        // Assert
        healer.Verify(manager => manager.HealForResetAsync("a1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SignOutAsync_WhenProfileExists_HealsStaleWorkBeforeSigningOut()
    {
        // Arrange
        var existing = new AgentProfile
        {
            ItemId = "a1",
            UserId = "u1",
            PresenceStatus = AgentPresenceStatus.Busy,
            QueueIds = ["q1"],
        };

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var healer = new Mock<IAgentWorkStateHealingService>();
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);
        var service = new AgentPresenceManagerService(agentManager.Object, [], [healer.Object], new Mock<IContactCenterEventPublisher>().Object, CreateDistributedLock().Object, clock.Object, new Mock<ILogger<AgentPresenceManagerService>>().Object);

        // Act
        await service.SignOutAsync("u1", TestContext.Current.CancellationToken);

        // Assert
        healer.Verify(manager => manager.HealForResetAsync("a1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetPresenceAsync_WhenStuckBusyWithoutLiveCall_HealsThenBecomesAvailable()
    {
        // Arrange
        var stuck = new AgentProfile
        {
            ItemId = "a1",
            UserId = "u1",
            PresenceStatus = AgentPresenceStatus.Busy,
            ActiveReservationId = "r1",
        };
        var healed = new AgentProfile
        {
            ItemId = "a1",
            UserId = "u1",
            PresenceStatus = AgentPresenceStatus.Available,
            ActiveReservationId = null,
        };

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.SetupSequence(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(stuck)
            .ReturnsAsync(healed);
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(healed);

        var healer = new Mock<IAgentWorkStateHealingService>();
        healer.Setup(h => h.HealForResetAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);
        var service = new AgentPresenceManagerService(agentManager.Object, [], [healer.Object], new Mock<IContactCenterEventPublisher>().Object, CreateDistributedLock().Object, clock.Object, new Mock<ILogger<AgentPresenceManagerService>>().Object);

        // Act
        var profile = await service.SetPresenceAsync("u1", AgentPresenceStatus.Available, "Ready", TestContext.Current.CancellationToken);

        // Assert
        healer.Verify(h => h.HealForResetAsync("a1", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(AgentPresenceStatus.Available, profile.PresenceStatus);
        Assert.Null(profile.RequestedPresenceStatus);
    }

    [Fact]
    public async Task SetPresenceAsync_WhenBusyWithLiveCall_DefersAvailabilityAfterHealing()
    {
        // Arrange
        var stuck = new AgentProfile
        {
            ItemId = "a1",
            UserId = "u1",
            PresenceStatus = AgentPresenceStatus.Busy,
            ActiveReservationId = "r1",
        };
        var stillBusy = new AgentProfile
        {
            ItemId = "a1",
            UserId = "u1",
            PresenceStatus = AgentPresenceStatus.Busy,
            ActiveReservationId = "r1",
        };

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.SetupSequence(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(stuck)
            .ReturnsAsync(stillBusy);
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(stillBusy);

        var healer = new Mock<IAgentWorkStateHealingService>();
        healer.Setup(h => h.HealForResetAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);
        var service = new AgentPresenceManagerService(agentManager.Object, [], [healer.Object], new Mock<IContactCenterEventPublisher>().Object, CreateDistributedLock().Object, clock.Object, new Mock<ILogger<AgentPresenceManagerService>>().Object);

        // Act
        var profile = await service.SetPresenceAsync("u1", AgentPresenceStatus.Available, null, TestContext.Current.CancellationToken);

        // Assert
        healer.Verify(h => h.HealForResetAsync("a1", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(AgentPresenceStatus.Busy, profile.PresenceStatus);
        Assert.Equal(AgentPresenceStatus.Available, profile.RequestedPresenceStatus);
    }

    private static Mock<IDistributedLock> CreateDistributedLock()
    {
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((null, true));

        return distributedLock;
    }
}
