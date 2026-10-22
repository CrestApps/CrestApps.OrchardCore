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
/// An agent whose page dropped across a restart signed in again while still on a call. Healing kept the call, but
/// sign-in made them Available anyway, and the dialer handed them a second call on top of the first.
/// </summary>
public sealed class LiveCallPresenceTests
{
    private static readonly DateTime _now = new(2026, 10, 21, 23, 7, 30, DateTimeKind.Utc);

    [Fact]
    public async Task SignInAsync_WhenOnALiveCall_KeepsTheAgentBusyAndDefersAvailable()
    {
        // Arrange
        // The agent's state was lost across the restart: it reads Available while a call still names them.
        var profile = CreateProfile(AgentPresenceStatus.Available);
        var service = CreatePresenceService(profile, OnCall(true));

        // Act
        var result = await service.SignInAsync("u1", ["q1"], [], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.Busy, result.PresenceStatus);
        Assert.Equal(AgentPresenceStatus.Available, result.RequestedPresenceStatus);
        Assert.Equal(["q1"], result.QueueIds);
    }

    [Fact]
    public async Task SignInAsync_WhenBusyOnALiveCall_StaysBusy()
    {
        // Arrange
        var profile = CreateProfile(AgentPresenceStatus.Busy);
        var service = CreatePresenceService(profile, OnCall(true));

        // Act
        var result = await service.SignInAsync("u1", ["q1"], [], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.Busy, result.PresenceStatus);
        Assert.Equal(AgentPresenceStatus.Available, result.RequestedPresenceStatus);
    }

    [Fact]
    public async Task SignInAsync_WhenNotOnACall_MakesTheAgentAvailable()
    {
        // Arrange
        var profile = CreateProfile(AgentPresenceStatus.Offline);
        var service = CreatePresenceService(profile, OnCall(false));

        // Act
        var result = await service.SignInAsync("u1", ["q1"], [], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.Available, result.PresenceStatus);
        Assert.Null(result.RequestedPresenceStatus);
    }

    [Fact]
    public async Task SetPresenceAsync_WhenAskingForAvailableOnALiveCall_PutsTheAgentOnTheCallAndDefers()
    {
        // Arrange
        var profile = CreateProfile(AgentPresenceStatus.Break);
        var service = CreatePresenceService(profile, OnCall(true));

        // Act
        var result = await service.SetPresenceAsync("u1", AgentPresenceStatus.Available, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.Busy, result.PresenceStatus);
        Assert.Equal(AgentPresenceStatus.Available, result.RequestedPresenceStatus);
    }

    [Fact]
    public async Task SetPresenceAsync_WhenAskingForABreakOnALiveCall_AppliesIt()
    {
        // Arrange
        // Only Available puts the agent back in front of routing; a break takes them out of it.
        var profile = CreateProfile(AgentPresenceStatus.Available);
        var service = CreatePresenceService(profile, OnCall(true));

        // Act
        var result = await service.SetPresenceAsync("u1", AgentPresenceStatus.Break, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.Break, result.PresenceStatus);
        Assert.Null(result.RequestedPresenceStatus);
    }

    [Fact]
    public async Task HasLiveCallAsync_WhenAVoiceCallIsConnected_ReturnsTrue()
    {
        // Arrange
        var healer = CreateHealer([CreateInteraction(InteractionChannel.Voice, InteractionStatus.Connected)]);

        // Act & Assert
        Assert.True(await healer.HasLiveCallAsync("a1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HasLiveCallAsync_WhenTheProviderSaysTheCallEnded_ReturnsFalse()
    {
        // Arrange
        // The call ended while nobody was listening; provider truth must not leave the agent stuck on it.
        var interaction = CreateInteraction(InteractionChannel.Voice, InteractionStatus.Connected);
        interaction.ProviderName = "Telnyx";
        interaction.ProviderInteractionId = "call-1";
        var ended = CreateInteraction(InteractionChannel.Voice, InteractionStatus.Ended);
        var healer = CreateHealer([interaction], refreshed: ended);

        // Act & Assert
        Assert.False(await healer.HasLiveCallAsync("a1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HasLiveCallAsync_WhenOnlyAChatIsActive_ReturnsFalse()
    {
        // Arrange
        var healer = CreateHealer([CreateInteraction(InteractionChannel.Chat, InteractionStatus.Connected)]);

        // Act & Assert
        Assert.False(await healer.HasLiveCallAsync("a1", TestContext.Current.CancellationToken));
    }

    private static IAgentWorkStateHealingService OnCall(bool onCall)
    {
        var healer = new Mock<IAgentWorkStateHealingService>();
        healer.Setup(h => h.HasLiveCallAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(onCall);

        return healer.Object;
    }

    private static AgentProfile CreateProfile(AgentPresenceStatus status)
        => new()
        {
            ItemId = "a1",
            UserId = "u1",
            PresenceStatus = status,
            QueueIds = ["q1"],
            AllowedQueueIds = ["q1"],
        };

    private static Interaction CreateInteraction(InteractionChannel channel, InteractionStatus status)
        => new Interaction
        {
            ItemId = "i1",
            Channel = channel,
            AgentId = "a1",
        }.RestorePersistedStatus(status);

    private static AgentWorkStateHealingService CreateHealer(IReadOnlyCollection<Interaction> interactions, Interaction refreshed = null)
    {
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(m => m.GetActiveByAgentIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(interactions);

        var synchronization = new Mock<IProviderCallStateSynchronizationService>();
        synchronization
            .Setup(s => s.RefreshInteractionAsync(It.IsAny<Interaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Interaction interaction, CancellationToken _) => refreshed ?? interaction);

        return new AgentWorkStateHealingService(
            Mock.Of<IAgentProfileManager>(),
            Mock.Of<IActivityReservationManager>(),
            Mock.Of<IActivityReservationService>(),
            Mock.Of<IQueueItemManager>(),
            interactionManager.Object,
            Mock.Of<IOmnichannelActivityManager>(),
            Mock.Of<IContactCenterWorkStateService>(),
            new Lazy<IProviderCallStateSynchronizationService>(() => synchronization.Object),
            Mock.Of<IClock>(),
            NullLogger<AgentWorkStateHealingService>.Instance);
    }

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
