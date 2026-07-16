using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class AgentAvailabilityRecoveryServiceTests
{
    private static readonly DateTime _now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RecoverAsync_WhenWrapUpIsWithinDeadline_DoesNotReleaseAgent()
    {
        // Arrange
        var agent = CreateAgent();
        var interaction = CreateInteraction(_now.AddMinutes(-5));
        var presenceManager = new Mock<IAgentPresenceManager>();
        var service = CreateService(agent, interaction, presenceManager);

        // Act
        var recovered = await service.RecoverAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, recovered);
        presenceManager.Verify(
            manager => manager.CompleteWorkAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RecoverAsync_WhenWrapUpDeadlineExpired_CompletesInteractionAndReleasesAgent()
    {
        // Arrange
        var agent = CreateAgent();
        var interaction = CreateInteraction(_now.AddMinutes(-16));
        var presenceManager = new Mock<IAgentPresenceManager>();
        presenceManager
            .Setup(manager => manager.CompleteWorkAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "a1", PresenceStatus = AgentPresenceStatus.Available });
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.ListPendingWrapUpsByAgentAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([interaction]);
        var service = CreateService(agent, interaction, presenceManager, interactionManager);

        // Act
        var recovered = await service.RecoverAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, recovered);
        Assert.Equal(_now, interaction.WrapUpCompletedUtc);
        interactionManager.Verify(
            manager => manager.UpdateAsync(interaction, null, It.IsAny<CancellationToken>()),
            Times.Once);
        presenceManager.Verify(
            manager => manager.CompleteWorkAsync("a1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RecoverAsync_WhenWrapUpHasNoInteraction_ReleasesOrphanedAgent()
    {
        // Arrange
        var agent = CreateAgent();
        var presenceManager = new Mock<IAgentPresenceManager>();
        presenceManager
            .Setup(manager => manager.CompleteWorkAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "a1", PresenceStatus = AgentPresenceStatus.Available });
        var service = CreateService(agent, null, presenceManager);

        // Act
        var recovered = await service.RecoverAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, recovered);
        presenceManager.Verify(
            manager => manager.CompleteWorkAsync("a1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RecoverAsync_WhenEveryPendingWrapUpExpired_CompletesAllInteractions()
    {
        // Arrange
        var agent = CreateAgent();
        var first = CreateInteraction(_now.AddMinutes(-20));
        var second = CreateInteraction(_now.AddMinutes(-16));
        second.ItemId = "i2";
        var presenceManager = new Mock<IAgentPresenceManager>();
        presenceManager
            .Setup(manager => manager.CompleteWorkAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "a1", PresenceStatus = AgentPresenceStatus.Available });
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.ListPendingWrapUpsByAgentAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([first, second]);
        var service = CreateService(agent, null, presenceManager, interactionManager);

        // Act
        var recovered = await service.RecoverAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, recovered);
        Assert.Equal(_now, first.WrapUpCompletedUtc);
        Assert.Equal(_now, second.WrapUpCompletedUtc);
        interactionManager.Verify(
            manager => manager.UpdateAsync(It.IsAny<Interaction>(), null, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task RecoverAsync_WhenRunAgainAfterRecovery_DoesNotRepeatTransitions()
    {
        // Arrange
        var agent = CreateAgent();
        var interaction = CreateInteraction(_now.AddMinutes(-16));
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.ListByPresenceAsync(AgentPresenceStatus.WrapUp, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => agent.PresenceStatus == AgentPresenceStatus.WrapUp ? [agent] : []);
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.ListPendingWrapUpsByAgentAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => interaction.WrapUpCompletedUtc.HasValue ? [] : [interaction]);
        var presenceManager = new Mock<IAgentPresenceManager>();
        presenceManager
            .Setup(manager => manager.CompleteWorkAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                agent.PresenceStatus = AgentPresenceStatus.Available;

                return agent;
            });
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);
        var service = new AgentAvailabilityRecoveryService(
            agentManager.Object,
            interactionManager.Object,
            presenceManager.Object,
            Options.Create(new AgentAvailabilityOptions()),
            clock.Object,
            new Mock<ILogger<AgentAvailabilityRecoveryService>>().Object);

        // Act
        var firstPass = await service.RecoverAsync(TestContext.Current.CancellationToken);
        var secondPass = await service.RecoverAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, firstPass);
        Assert.Equal(0, secondPass);
        interactionManager.Verify(
            manager => manager.UpdateAsync(interaction, null, It.IsAny<CancellationToken>()),
            Times.Once);
        presenceManager.Verify(
            manager => manager.CompleteWorkAsync("a1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RecoverAsync_WhenOneAgentLockIsContended_ContinuesWithRemainingAgents()
    {
        // Arrange
        var first = CreateAgent();
        var second = new AgentProfile
        {
            ItemId = "a2",
            PresenceStatus = AgentPresenceStatus.WrapUp,
        };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.ListByPresenceAsync(AgentPresenceStatus.WrapUp, It.IsAny<CancellationToken>()))
            .ReturnsAsync([first, second]);
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.ListPendingWrapUpsByAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var presenceManager = new Mock<IAgentPresenceManager>();
        presenceManager
            .Setup(manager => manager.CompleteWorkAsync("a1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The profile is locked."));
        presenceManager
            .Setup(manager => manager.CompleteWorkAsync("a2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "a2", PresenceStatus = AgentPresenceStatus.Available });
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);
        var service = new AgentAvailabilityRecoveryService(
            agentManager.Object,
            interactionManager.Object,
            presenceManager.Object,
            Options.Create(new AgentAvailabilityOptions()),
            clock.Object,
            new Mock<ILogger<AgentAvailabilityRecoveryService>>().Object);

        // Act
        var recovered = await service.RecoverAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, recovered);
        presenceManager.Verify(
            manager => manager.CompleteWorkAsync("a2", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static AgentAvailabilityRecoveryService CreateService(
        AgentProfile agent,
        Interaction interaction,
        Mock<IAgentPresenceManager> presenceManager,
        Mock<IInteractionManager> interactionManager = null)
    {
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.ListByPresenceAsync(AgentPresenceStatus.WrapUp, It.IsAny<CancellationToken>()))
            .ReturnsAsync([agent]);

        if (interactionManager is null)
        {
            interactionManager = new Mock<IInteractionManager>();
            interactionManager
                .Setup(manager => manager.ListPendingWrapUpsByAgentAsync("a1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(interaction is null ? [] : [interaction]);
        }

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return new AgentAvailabilityRecoveryService(
            agentManager.Object,
            interactionManager.Object,
            presenceManager.Object,
            Options.Create(new AgentAvailabilityOptions()),
            clock.Object,
            new Mock<ILogger<AgentAvailabilityRecoveryService>>().Object);
    }

    private static AgentProfile CreateAgent()
    {
        return new AgentProfile
        {
            ItemId = "a1",
            PresenceStatus = AgentPresenceStatus.WrapUp,
        };
    }

    private static Interaction CreateInteraction(DateTime wrapUpStartedUtc)
    {
        return new Interaction
        {
            ItemId = "i1",
            AgentId = "a1",
            WrapUpStartedUtc = wrapUpStartedUtc,
        };
    }
}
