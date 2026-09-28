using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class RoutingStrategyTests
{
    private static readonly DateTime _now = new(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RoundRobin_SelectsTheAgentWhoLeastRecentlyFinishedWork()
    {
        // Arrange
        // Fairness is about who last finished something, not who last had an offer pushed at them: an agent who
        // declined or missed an offer would otherwise hold their place at the front of the rotation.
        var queue = new ActivityQueue { ItemId = "q1", RoutingStrategy = QueueRoutingStrategy.RoundRobin };
        var item = new QueueItem { ItemId = "i1", QueueId = "q1" };
        var justFinished = new AgentProfile { ItemId = "a1", LastWorkCompletedUtc = _now };
        var finishedLongAgo = new AgentProfile { ItemId = "a2", LastWorkCompletedUtc = _now.AddMinutes(-30) };

        var service = new ActivityRoutingService([new RoundRobinRoutingStrategy()]);

        // Act
        var decision = await service.SelectAgentAsync(queue, item, [Availability(justFinished), Availability(finishedLongAgo)], TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.Succeeded);
        Assert.Same(finishedLongAgo, decision.Agent);
    }

    [Fact]
    public async Task RoundRobin_FallsBackToTheLastAssignment_WhenNobodyHasFinishedWorkYet()
    {
        // Arrange
        var queue = new ActivityQueue { ItemId = "q1", RoutingStrategy = QueueRoutingStrategy.RoundRobin };
        var item = new QueueItem { ItemId = "i1", QueueId = "q1" };
        var recentlyAssigned = new AgentProfile { ItemId = "a1", LastAssignedUtc = _now };
        var leastRecentlyAssigned = new AgentProfile { ItemId = "a2", LastAssignedUtc = _now.AddMinutes(-30) };

        var service = new ActivityRoutingService([new RoundRobinRoutingStrategy()]);

        // Act
        var decision = await service.SelectAgentAsync(queue, item, [Availability(recentlyAssigned), Availability(leastRecentlyAssigned)], TestContext.Current.CancellationToken);

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

        var service = new ActivityRoutingService([new LeastBusyRoutingStrategy()]);

        // Act
        var decision = await service.SelectAgentAsync(
            queue,
            item,
            [Availability(busyAgent, activeInteractions: 2), Availability(freeAgent, activeInteractions: 0)],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.Succeeded);
        Assert.Same(freeAgent, decision.Agent);
    }

    [Fact]
    public async Task LongestIdle_MeasuresIdleness_NotTheLastPresenceChange()
    {
        // Arrange
        // The agent who just hung up has the more recent presence change but is not the more idle one; ranking on
        // presence alone kept handing work back to whoever had most recently finished a call.
        var queue = new ActivityQueue { ItemId = "q1", RoutingStrategy = QueueRoutingStrategy.LongestIdle };
        var item = new QueueItem { ItemId = "i1", QueueId = "q1" };
        var justOffACall = new AgentProfile
        {
            ItemId = "a1",
            PresenceChangedUtc = _now.AddMinutes(-30),
            IdleSinceUtc = _now,
        };
        var genuinelyIdle = new AgentProfile
        {
            ItemId = "a2",
            PresenceChangedUtc = _now,
            IdleSinceUtc = _now.AddMinutes(-30),
        };

        var service = new ActivityRoutingService([new LongestIdleRoutingStrategy()]);

        // Act
        var decision = await service.SelectAgentAsync(queue, item, [Availability(justOffACall), Availability(genuinelyIdle)], TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.Succeeded);
        Assert.Same(genuinelyIdle, decision.Agent);
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
        var decision = await service.SelectAgentAsync(queue, item, [Availability(longestIdleAgent), Availability(stickyAgent)], TestContext.Current.CancellationToken);

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
        var longestIdleButJustFinished = new AgentProfile { ItemId = "a1", IdleSinceUtc = _now.AddMinutes(-30), LastWorkCompletedUtc = _now };
        var newerButFinishedLongAgo = new AgentProfile { ItemId = "a2", IdleSinceUtc = _now, LastWorkCompletedUtc = _now.AddMinutes(-30) };

        var service = new ActivityRoutingService([new LongestIdleRoutingStrategy(), new RoundRobinRoutingStrategy()]);

        // Act
        var decision = await service.SelectAgentAsync(queue, item, [Availability(longestIdleButJustFinished), Availability(newerButFinishedLongAgo)], TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.Succeeded);
        Assert.Same(newerButFinishedLongAgo, decision.Agent);
    }

    private static AgentAvailability Availability(AgentProfile agent, int activeInteractions = 0)
        => new() { Agent = agent, ActiveInteractionCount = activeInteractions };
}
