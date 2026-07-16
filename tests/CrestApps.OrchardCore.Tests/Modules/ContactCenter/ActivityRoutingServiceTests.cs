using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
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
            [missingSkillAgent, skilledAgent],
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
            [newestAgent, longestIdleAgent],
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

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(m => m.CountActiveByAgentAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        interactionManager
            .Setup(m => m.CountActiveByAgentAsync("a2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var service = CreateServiceWithCapacity(interactionManager);
        var queue = new ActivityQueue { ItemId = "q1" };
        var item = new QueueItem { ItemId = "i1", QueueId = "q1" };

        // Act
        var decision = await service.SelectAgentAsync(
            queue,
            item,
            [longestIdleButBusyAgent, freeAgent],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.Succeeded);
        Assert.Same(freeAgent, decision.Agent);
        Assert.False(decision.Candidates.Single(candidate => candidate.Agent == longestIdleButBusyAgent).IsEligible);
    }

    private static ActivityRoutingService CreateService()
    {
        return new ActivityRoutingService(
        [
            new RequiredSkillsRoutingStrategy(),
            new LongestIdleRoutingStrategy(),
        ]);
    }

    private static ActivityRoutingService CreateServiceWithCapacity(Mock<IInteractionManager> interactionManager)
    {
        return new ActivityRoutingService(
        [
            new RequiredSkillsRoutingStrategy(),
            new CapacityRoutingStrategy(interactionManager.Object),
            new LongestIdleRoutingStrategy(),
        ]);
    }
}
