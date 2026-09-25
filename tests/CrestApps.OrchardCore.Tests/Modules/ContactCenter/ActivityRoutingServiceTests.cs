using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

using OrchardCore.Modules;
using Moq;
namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ActivityRoutingServiceTests
{
    [Fact]
    public async Task SelectAgentAsync_WhenQueueRequiresSkills_RejectsMissingSkillCandidates()
    {
        // Arrange
        var service = CreateService();
        var queue = new ActivityQueue { ItemId = "q1", RequiredSkills = ["billing"] };
        var item = new QueueItem { ItemId = "i1", QueueId = "q1" };
        var missingSkillAgent = new AgentProfile { ItemId = "a1", Skills = ["general"] };
        var skilledAgent = new AgentProfile { ItemId = "a2", Skills = ["billing"] };

        // Act
        var decision = await service.SelectAgentAsync(
            queue,
            item,
            [Availability(missingSkillAgent), Availability(skilledAgent)],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.Succeeded);
        Assert.Same(skilledAgent, decision.Agent);
        Assert.False(decision.Candidates.Single(candidate => candidate.Agent == missingSkillAgent).IsEligible);
    }

    [Fact]
    public async Task SelectAgentAsync_WhenMultipleAgentsEligible_SelectsLongestIdle()
    {
        // Arrange
        var service = CreateService();
        var queue = new ActivityQueue { ItemId = "q1" };
        var item = new QueueItem { ItemId = "i1", QueueId = "q1" };
        var newestAgent = new AgentProfile { ItemId = "a1", PresenceChangedUtc = new DateTime(2026, 1, 2) };
        var longestIdleAgent = new AgentProfile { ItemId = "a2", PresenceChangedUtc = new DateTime(2026, 1, 1) };

        // Act
        var decision = await service.SelectAgentAsync(
            queue,
            item,
            [Availability(newestAgent), Availability(longestIdleAgent)],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.Succeeded);
        Assert.Same(longestIdleAgent, decision.Agent);
    }

    [Fact]
    public async Task SelectAgentAsync_WhenLongestIdleAgentIsAtCapacity_SelectsAgentWithSpareCapacity()
    {
        // Arrange
        var longestIdleButBusyAgent = new AgentProfile
        {
            ItemId = "a1",
            MaxConcurrentInteractions = 1,
            PresenceChangedUtc = new DateTime(2026, 1, 1),
        };

        var freeAgent = new AgentProfile
        {
            ItemId = "a2",
            MaxConcurrentInteractions = 1,
            PresenceChangedUtc = new DateTime(2026, 1, 2),
        };

        var service = CreateServiceWithCapacity();
        var queue = new ActivityQueue { ItemId = "q1" };
        var item = new QueueItem { ItemId = "i1", QueueId = "q1" };

        // Act
        var decision = await service.SelectAgentAsync(
            queue,
            item,
            [Availability(longestIdleButBusyAgent, activeInteractions: 1), Availability(freeAgent, activeInteractions: 0)],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.Succeeded);
        Assert.Same(freeAgent, decision.Agent);
        Assert.False(decision.Candidates.Single(candidate => candidate.Agent == longestIdleButBusyAgent).IsEligible);
    }

    [Fact]
    public async Task SelectAgentAsync_NeverPicksAnAgentTheItemExcludes_EvenWhenTheyAreTheStickyAgent()
    {
        // Arrange
        // The agent who just transferred the call away is also the one the customer last worked with, and the one who
        // has been idle longest: every preference points back at them.
        var service = new ActivityRoutingService(
        [
            new StickyAgentRoutingStrategy(),
            new LongestIdleRoutingStrategy(),
        ]);
        var queue = new ActivityQueue { ItemId = "q1", PreferStickyAgent = true };
        var item = new QueueItem { ItemId = "i1", QueueId = "q1", StickyAgentUserId = "u1", ExcludedAgentIds = ["a1"] };
        var transferringAgent = new AgentProfile { ItemId = "a1", UserId = "u1", PresenceChangedUtc = new DateTime(2026, 1, 1) };
        var otherAgent = new AgentProfile { ItemId = "a2", UserId = "u2", PresenceChangedUtc = new DateTime(2026, 1, 2) };

        // Act
        var decision = await service.SelectAgentAsync(
            queue,
            item,
            [Availability(transferringAgent), Availability(otherAgent)],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.Succeeded);
        Assert.Same(otherAgent, decision.Agent);
        Assert.False(decision.Candidates.Single(candidate => candidate.Agent == transferringAgent).IsEligible);
    }

    [Fact]
    public async Task SelectAgentAsync_WhenTheOnlyAvailableAgentIsExcluded_AssignsNobody()
    {
        // Arrange
        var service = CreateService();
        var queue = new ActivityQueue { ItemId = "q1" };
        var item = new QueueItem { ItemId = "i1", QueueId = "q1", ExcludedAgentIds = ["a1"] };
        var transferringAgent = new AgentProfile { ItemId = "a1", UserId = "u1" };

        // Act
        var decision = await service.SelectAgentAsync(
            queue,
            item,
            [Availability(transferringAgent)],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(decision.Succeeded);
        Assert.Null(decision.Agent);
    }

    private static ActivityRoutingService CreateService()
    {
        return new ActivityRoutingService(
        [
            new RequiredSkillsRoutingStrategy(Mock.Of<IClock>()),
            new LongestIdleRoutingStrategy(),
        ]);
    }

    private static ActivityRoutingService CreateServiceWithCapacity()
    {
        return new ActivityRoutingService(
        [
            new RequiredSkillsRoutingStrategy(Mock.Of<IClock>()),
            new CapacityRoutingStrategy(),
            new LongestIdleRoutingStrategy(),
        ]);
    }

    // Routing now reads the availability snapshot the caller already produced, so a candidate carries its own
    // load rather than the strategy querying for it per agent.
    private static AgentAvailability Availability(AgentProfile agent, int activeInteractions = 0)
        => new() { Agent = agent, ActiveInteractionCount = activeInteractions };
}
