using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The capacity gate reads each candidate's load from the availability snapshot the caller already produced.
/// Querying it per candidate made a single assignment cost one round trip for every signed-in agent, which grows
/// with the size of the queue rather than with the work being assigned.
/// </summary>
public sealed class CapacityRoutingStrategyTests
{
    [Fact]
    public async Task ApplyAsync_WhenAgentAtCapacity_RejectsCandidate()
    {
        // Arrange
        var agent = new AgentProfile { ItemId = "a1", MaxConcurrentInteractions = 1 };
        var context = CreateContext(agent, activeInteractions: 1);
        var strategy = new CapacityRoutingStrategy();

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
        var context = CreateContext(agent, activeInteractions: 2);
        var strategy = new CapacityRoutingStrategy();

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
        var context = CreateContext(agent, activeInteractions: 1);
        var strategy = new CapacityRoutingStrategy();

        // Act
        await strategy.ApplyAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(context.Candidates.Single().IsEligible);
    }

    [Fact]
    public async Task ApplyAsync_LeavesAnAlreadyIneligibleCandidateUntouched()
    {
        // Arrange
        var agent = new AgentProfile { ItemId = "a1", MaxConcurrentInteractions = 1 };
        var context = CreateContext(agent, activeInteractions: 5);
        var candidate = context.Candidates.Single();
        candidate.IsEligible = false;
        var strategy = new CapacityRoutingStrategy();

        // Act
        await strategy.ApplyAsync(context, TestContext.Current.CancellationToken);

        // Assert
        // A candidate an earlier strategy already rejected keeps that rejection and gains no capacity reason.
        Assert.False(candidate.IsEligible);
        Assert.Empty(candidate.Reasons);
    }

    [Fact]
    public async Task ApplyAsync_WithNoAvailabilitySnapshot_TreatsTheAgentAsIdle()
    {
        // Arrange
        // A caller that built the candidate from a bare profile has no counts to offer; the gate must not then
        // reject everyone.
        var agent = new AgentProfile { ItemId = "a1", MaxConcurrentInteractions = 1 };
        var queue = new ActivityQueue { ItemId = "q1" };
        var item = new QueueItem { ItemId = "i1", QueueId = "q1" };
        var context = new ActivityRoutingContext(queue, item, [new ActivityRoutingCandidate(agent)]);
        var strategy = new CapacityRoutingStrategy();

        // Act
        await strategy.ApplyAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(context.Candidates.Single().IsEligible);
    }

    private static ActivityRoutingContext CreateContext(AgentProfile agent, int activeInteractions)
    {
        var queue = new ActivityQueue { ItemId = "q1" };
        var item = new QueueItem { ItemId = "i1", QueueId = "q1" };
        var availability = new AgentAvailability
        {
            Agent = agent,
            ActiveInteractionCount = activeInteractions,
        };

        return new ActivityRoutingContext(queue, item, [new ActivityRoutingCandidate(availability)]);
    }
}
