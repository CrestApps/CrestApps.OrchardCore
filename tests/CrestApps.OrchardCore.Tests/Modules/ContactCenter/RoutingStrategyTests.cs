using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class RoutingStrategyTests
{
    private static readonly DateTime _now = new(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RoundRobin_SelectsLeastRecentlyAssignedAgent()
    {
        // Arrange
        var queue = new ActivityQueue { ItemId = "q1", RoutingStrategy = QueueRoutingStrategy.RoundRobin };
        var item = new QueueItem { ItemId = "i1", QueueId = "q1" };
        var recentlyAssigned = new AgentProfile { ItemId = "a1", LastAssignedUtc = _now };
        var leastRecentlyAssigned = new AgentProfile { ItemId = "a2", LastAssignedUtc = _now.AddMinutes(-30) };

        var service = new ActivityRoutingService([new RoundRobinRoutingStrategy()]);

        // Act
        var decision = await service.SelectAgentAsync(queue, item, [recentlyAssigned, leastRecentlyAssigned], TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.Succeeded);
        Assert.Same(leastRecentlyAssigned, decision.Agent);
    }

    [Fact]
    public async Task LeastBusy_SelectsAgentWithFewestActiveInteractions()
    {
        // Arrange
        var queue = new ActivityQueue { ItemId = "q1", RoutingStrategy = QueueRoutingStrategy.LeastBusy };
        var item = new QueueItem { ItemId = "i1", QueueId = "q1" };
        var busyAgent = new AgentProfile { ItemId = "a1" };
        var freeAgent = new AgentProfile { ItemId = "a2" };

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.CountActiveByAgentAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(2);
        interactionManager.Setup(m => m.CountActiveByAgentAsync("a2", It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var service = new ActivityRoutingService([new LeastBusyRoutingStrategy(interactionManager.Object)]);

        // Act
        var decision = await service.SelectAgentAsync(queue, item, [busyAgent, freeAgent], TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.Succeeded);
        Assert.Same(freeAgent, decision.Agent);
    }

    [Fact]
    public async Task StickyAgent_PreferredWhenQueueEnablesStickyRouting()
    {
        // Arrange
        var queue = new ActivityQueue { ItemId = "q1", RoutingStrategy = QueueRoutingStrategy.LongestIdle, PreferStickyAgent = true };
        var item = new QueueItem { ItemId = "i1", QueueId = "q1", StickyAgentUserId = "u2" };
        var longestIdleAgent = new AgentProfile { ItemId = "a1", UserId = "u1", PresenceChangedUtc = _now.AddMinutes(-10) };
        var stickyAgent = new AgentProfile { ItemId = "a2", UserId = "u2", PresenceChangedUtc = _now };

        var service = new ActivityRoutingService([new StickyAgentRoutingStrategy(), new LongestIdleRoutingStrategy()]);

        // Act
        var decision = await service.SelectAgentAsync(queue, item, [longestIdleAgent, stickyAgent], TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.Succeeded);
        Assert.Same(stickyAgent, decision.Agent);
    }

    [Fact]
    public async Task PrimaryStrategy_OnlyTheSelectedQueueStrategyScores()
    {
        // Arrange
        var queue = new ActivityQueue { ItemId = "q1", RoutingStrategy = QueueRoutingStrategy.RoundRobin };
        var item = new QueueItem { ItemId = "i1", QueueId = "q1" };
        var longestIdleButRecentlyAssigned = new AgentProfile { ItemId = "a1", PresenceChangedUtc = _now.AddMinutes(-30), LastAssignedUtc = _now };
        var newerButLeastRecentlyAssigned = new AgentProfile { ItemId = "a2", PresenceChangedUtc = _now, LastAssignedUtc = _now.AddMinutes(-30) };

        var service = new ActivityRoutingService([new LongestIdleRoutingStrategy(), new RoundRobinRoutingStrategy()]);

        // Act
        var decision = await service.SelectAgentAsync(queue, item, [longestIdleButRecentlyAssigned, newerButLeastRecentlyAssigned], TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.Succeeded);
        Assert.Same(newerButLeastRecentlyAssigned, decision.Agent);
    }
}
