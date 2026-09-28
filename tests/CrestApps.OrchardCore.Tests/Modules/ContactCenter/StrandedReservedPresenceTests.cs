using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// An agent left Reserved after the offer that reserved them was settled without releasing them. Reserved means an
/// offer is ringing; with none left, nothing ever moves them on, and every change they ask for used to be deferred
/// behind an offer that no longer exists -- so they could take no calls at all.
/// </summary>
public sealed class StrandedReservedPresenceTests
{
    private static readonly DateTime _now = new(2026, 9, 25, 19, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SetPresenceAsync_WhenReservedWithNoOfferLeft_AppliesTheRequestNow()
    {
        // Arrange
        var healer = new Mock<IAgentWorkStateHealingService>();
        healer.Setup(h => h.HasPendingOfferAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var service = CreatePresenceService(Stranded(), healer.Object);

        // Act
        var profile = await service.SetPresenceAsync("u1", AgentPresenceStatus.Available, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.Available, profile.PresenceStatus);
        Assert.Null(profile.RequestedPresenceStatus);
    }

    [Fact]
    public async Task SetPresenceAsync_WhenReservedWhileAnOfferIsStillRinging_StillDefersTheRequest()
    {
        // Arrange
        var healer = new Mock<IAgentWorkStateHealingService>();
        healer.Setup(h => h.HasPendingOfferAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var service = CreatePresenceService(Stranded(), healer.Object);

        // Act
        var profile = await service.SetPresenceAsync("u1", AgentPresenceStatus.Available, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.Reserved, profile.PresenceStatus);
        Assert.Equal(AgentPresenceStatus.Available, profile.RequestedPresenceStatus);
    }

    [Fact]
    public async Task SetPresenceAsync_WhenWrappingUp_StillDefersTheRequest()
    {
        // Arrange
        // After-call work has no offer behind it by design; it ends when the agent finishes it.
        var wrapping = Stranded();
        wrapping.PresenceStatus = AgentPresenceStatus.WrapUp;
        var healer = new Mock<IAgentWorkStateHealingService>();
        var service = CreatePresenceService(wrapping, healer.Object);

        // Act
        var profile = await service.SetPresenceAsync("u1", AgentPresenceStatus.Available, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.WrapUp, profile.PresenceStatus);
        Assert.Equal(AgentPresenceStatus.Available, profile.RequestedPresenceStatus);
    }

    [Fact]
    public async Task HasPendingOfferAsync_ReadsWhetherAPendingReservationNamesTheAgent()
    {
        // Arrange
        var reservations = new Mock<IActivityReservationManager>();
        reservations
            .Setup(m => m.FindPendingByAgentAsync("ringing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityReservation { ItemId = "r1", AgentId = "ringing" });
        var healer = new AgentWorkStateHealingService(
            Mock.Of<IAgentProfileManager>(),
            reservations.Object,
            Mock.Of<IActivityReservationService>(),
            Mock.Of<IQueueItemManager>(),
            Mock.Of<IInteractionManager>(),
            Mock.Of<IOmnichannelActivityManager>(),
            Mock.Of<IContactCenterWorkStateService>(),
            new Lazy<IProviderCallStateSynchronizationService>(Mock.Of<IProviderCallStateSynchronizationService>),
            Mock.Of<IClock>(),
            NullLogger<AgentWorkStateHealingService>.Instance);

        // Act & Assert
        Assert.True(await healer.HasPendingOfferAsync("ringing", TestContext.Current.CancellationToken));
        Assert.False(await healer.HasPendingOfferAsync("idle", TestContext.Current.CancellationToken));
    }

    // Reserved with no reservation of its own: the offer it was reserved for was cancelled when the caller hung up,
    // and the agent was never released from it.
    private static AgentProfile Stranded()
        => new()
        {
            ItemId = "a1",
            UserId = "u1",
            PresenceStatus = AgentPresenceStatus.Reserved,
            QueueIds = ["q1"],
            AllowedQueueIds = ["q1"],
        };

    private static AgentPresenceManagerService CreatePresenceService(AgentProfile profile, IAgentWorkStateHealingService healer)
    {
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((null, true));

        return new AgentPresenceManagerService(
            agentManager.Object,
            [],
            healer,
            new EnforcingAgentEntitlementPolicy(),
            AgentStateAuditTestDoubles.CreateTransitions(clock: clock.Object),
            new Mock<IContactCenterEventPublisher>().Object,
            distributedLock.Object,
            clock.Object,
            new Mock<ILogger<AgentPresenceManagerService>>().Object);
    }
}
