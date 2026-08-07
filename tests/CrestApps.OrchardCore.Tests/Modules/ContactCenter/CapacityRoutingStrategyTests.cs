using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class CapacityRoutingStrategyTests
{
    [Fact]
    public async Task ApplyAsync_WhenAgentAtCapacity_RejectsCandidate()
    {
        // Arrange
        var agent = new AgentProfile { ItemId = "a1", MaxConcurrentInteractions = 1 };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(m => m.CountActiveByAgentAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var context = CreateContext(agent);
        var strategy = new CapacityRoutingStrategy(interactionManager.Object);

        // Act
        await strategy.ApplyAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(context.Candidates.Single().IsEligible);
    }

    [Fact]
    public async Task ApplyAsync_WhenAgentHasSpareCapacity_KeepsCandidateEligible()
    {
        // Arrange
        var agent = new AgentProfile { ItemId = "a1", MaxConcurrentInteractions = 3 };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(m => m.CountActiveByAgentAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var context = CreateContext(agent);
        var strategy = new CapacityRoutingStrategy(interactionManager.Object);

        // Act
        await strategy.ApplyAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(context.Candidates.Single().IsEligible);
    }

    [Fact]
    public async Task ApplyAsync_WhenCapacityIsUnset_TreatsCapacityAsOne()
    {
        // Arrange
        var agent = new AgentProfile { ItemId = "a1", MaxConcurrentInteractions = 0 };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(m => m.CountActiveByAgentAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var context = CreateContext(agent);
        var strategy = new CapacityRoutingStrategy(interactionManager.Object);

        // Act
        await strategy.ApplyAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(context.Candidates.Single().IsEligible);
    }

    [Fact]
    public async Task ApplyAsync_DoesNotCountAlreadyIneligibleCandidates()
    {
        // Arrange
        var agent = new AgentProfile { ItemId = "a1", MaxConcurrentInteractions = 1 };
        var interactionManager = new Mock<IInteractionManager>();
        var context = CreateContext(agent);
        context.Candidates.Single().IsEligible = false;
        var strategy = new CapacityRoutingStrategy(interactionManager.Object);

        // Act
        await strategy.ApplyAsync(context, TestContext.Current.CancellationToken);

        // Assert
        interactionManager.Verify(
            m => m.CountActiveByAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ActivityRoutingContext CreateContext(AgentProfile agent)
    {
        var queue = new ActivityQueue { ItemId = "q1" };
        var item = new QueueItem { ItemId = "i1", QueueId = "q1" };

        return new ActivityRoutingContext(queue, item, [new ActivityRoutingCandidate(agent)]);
    }
}
