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
    public async Task SelectAgentAsync_DoesNotPickTheAgentWhoTransferredTheCallAway_WhileAnotherCanTakeIt_EvenWhenTheyAreTheStickyAgent()
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
    public async Task SelectAgentAsync_WhenTheOnlyAvailableAgentTransferredTheCallAway_OffersItBackToThem()
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
        // Nobody else can take the call; the caller is better rung back to them than left on hold with an agent free.
        Assert.True(decision.Succeeded);
        Assert.Same(transferringAgent, decision.Agent);
    }

    [Fact]
    public async Task SelectAgentAsync_WhenTheOnlyOtherAgentDeclines_OffersTheCallBackToTheAgentWhoTransferredIt()
    {
        // Arrange
        // Live: the caller was transferred into the queue by a2, offered to a1, and a1 declined; the caller then waited
        // until they hung up, with a2 free the whole time. a2 is next, and a1 again only when a2 cannot take the call.
        var service = CreateService();
        var queue = new ActivityQueue { ItemId = "q1" };
        var item = new QueueItem { ItemId = "i1", QueueId = "q1", ExcludedAgentIds = ["a2"], DeclinedAgentIds = ["a1"], LastDeclinedUtc = _now };
        var decliner = new AgentProfile { ItemId = "a1", UserId = "u1", IdleSinceUtc = new DateTime(2026, 1, 2) };
        var transferringAgent = new AgentProfile { ItemId = "a2", UserId = "u2", IdleSinceUtc = new DateTime(2026, 1, 1) };

        // Act
        var withBoth = await service.SelectAgentAsync(queue, item, [Availability(transferringAgent), Availability(decliner)], TestContext.Current.CancellationToken);
        var withTheTransferrerOnly = await service.SelectAgentAsync(queue, item, [Availability(transferringAgent)], TestContext.Current.CancellationToken);
        var withTheDeclinerOnly = await service.SelectAgentAsync(queue, item, [Availability(decliner)], TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(transferringAgent, withBoth.Agent);
        Assert.Same(transferringAgent, withTheTransferrerOnly.Agent);
        Assert.Same(decliner, withTheDeclinerOnly.Agent);
    }

    [Fact]
    public async Task SelectAgentAsync_AnEarlierDecliner_ComesBeforeTheAgentWhoTransferredTheCallAway()
    {
        // Arrange
        // a3 transferred the call in; a1 declined it, then a2 did. a1 has waited longest since turning it down, a3 comes
        // after the decliners who are not the latest, and a2, who has just declined, comes last.
        var service = CreateService();
        var queue = new ActivityQueue { ItemId = "q1" };
        var item = new QueueItem { ItemId = "i1", QueueId = "q1", ExcludedAgentIds = ["a3"], DeclinedAgentIds = ["a1", "a2"], LastDeclinedUtc = _now };
        var earlierDecliner = new AgentProfile { ItemId = "a1", UserId = "u1", IdleSinceUtc = new DateTime(2026, 1, 3) };
        var latestDecliner = new AgentProfile { ItemId = "a2", UserId = "u2", IdleSinceUtc = new DateTime(2026, 1, 1) };
        var transferringAgent = new AgentProfile { ItemId = "a3", UserId = "u3", IdleSinceUtc = new DateTime(2026, 1, 2) };

        // Act
        var withAll = await service.SelectAgentAsync(
            queue,
            item,
            [Availability(latestDecliner), Availability(transferringAgent), Availability(earlierDecliner)],
            TestContext.Current.CancellationToken);
        var withoutTheEarlierDecliner = await service.SelectAgentAsync(
            queue,
            item,
            [Availability(latestDecliner), Availability(transferringAgent)],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(earlierDecliner, withAll.Agent);
        Assert.Same(transferringAgent, withoutTheEarlierDecliner.Agent);
    }

    [Fact]
    public async Task SelectAgentAsync_TheAgentWhoTransferredTheCallAway_AndDeclinedIt_ComesBeforeTheAgentWhoDeclinedLast()
    {
        // Arrange
        // a2 sent the call away, was offered it back as the only one free, and declined; a1 declined after that. a1 has
        // just turned it down, so a2 is next.
        var service = CreateService();
        var queue = new ActivityQueue { ItemId = "q1" };
        var item = new QueueItem { ItemId = "i1", QueueId = "q1", ExcludedAgentIds = ["a2"], DeclinedAgentIds = ["a2", "a1"], LastDeclinedUtc = _now };
        var laterDecliner = new AgentProfile { ItemId = "a1", UserId = "u1" };
        var transferringAgent = new AgentProfile { ItemId = "a2", UserId = "u2" };

        // Act
        var decision = await service.SelectAgentAsync(queue, item, [Availability(transferringAgent), Availability(laterDecliner)], TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(transferringAgent, decision.Agent);
    }

    [Fact]
    public async Task SelectAgentAsync_SkipsAnAgentWhoDeclinedTheItem_ThoughTheyHaveBeenIdleLongest()
    {
        // Arrange
        // Live: the longest-idle agent declined, was still the longest idle a moment later, and was offered the same
        // caller again and again while the other Available agent was never rung.
        var service = CreateService();
        var queue = new ActivityQueue { ItemId = "q1" };
        var item = new QueueItem { ItemId = "i1", QueueId = "q1", DeclinedAgentIds = ["a1"], LastDeclinedUtc = _now };
        var decliner = new AgentProfile { ItemId = "a1", UserId = "u1", IdleSinceUtc = new DateTime(2026, 1, 1) };
        var nextInLine = new AgentProfile { ItemId = "a2", UserId = "u2", IdleSinceUtc = new DateTime(2026, 1, 2) };

        // Act
        var decision = await service.SelectAgentAsync(
            queue,
            item,
            [Availability(decliner), Availability(nextInLine)],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.Succeeded);
        Assert.Same(nextInLine, decision.Agent);
        Assert.False(decision.Candidates.Single(candidate => candidate.Agent == decliner).IsEligible);
    }

    [Fact]
    public async Task SelectAgentAsync_AmongTheAgentsWhoHaveNotDeclined_TheQueuesStrategyStillChooses()
    {
        // Arrange
        // A decline only takes the decliner out; which of the others is offered the call is still the routing
        // strategy's choice, not the order the candidates happened to be listed in.
        var service = CreateService();
        var queue = new ActivityQueue { ItemId = "q1" };
        var item = new QueueItem { ItemId = "i1", QueueId = "q1", DeclinedAgentIds = ["a1"], LastDeclinedUtc = _now };
        var decliner = new AgentProfile { ItemId = "a1", UserId = "u1", IdleSinceUtc = new DateTime(2026, 1, 1) };
        var listedFirst = new AgentProfile { ItemId = "a2", UserId = "u2", IdleSinceUtc = new DateTime(2026, 1, 3) };
        var longestIdle = new AgentProfile { ItemId = "a3", UserId = "u3", IdleSinceUtc = new DateTime(2026, 1, 2) };

        // Act
        var decision = await service.SelectAgentAsync(
            queue,
            item,
            [Availability(decliner), Availability(listedFirst), Availability(longestIdle)],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.Succeeded);
        Assert.Same(longestIdle, decision.Agent);
        Assert.True(decision.Candidates.Single(candidate => candidate.Agent == listedFirst).IsEligible);
    }

    [Fact]
    public async Task SelectAgentAsync_AStickyAgentWhoDeclined_IsNotPreferred()
    {
        // Arrange
        var service = new ActivityRoutingService(
        [
            new StickyAgentRoutingStrategy(),
            new LongestIdleRoutingStrategy(),
        ]);
        var queue = new ActivityQueue { ItemId = "q1", PreferStickyAgent = true };
        var item = new QueueItem { ItemId = "i1", QueueId = "q1", StickyAgentUserId = "u1", DeclinedAgentIds = ["a1"], LastDeclinedUtc = _now };
        var stickyAgent = new AgentProfile { ItemId = "a1", UserId = "u1", IdleSinceUtc = new DateTime(2026, 1, 1) };
        var otherAgent = new AgentProfile { ItemId = "a2", UserId = "u2", IdleSinceUtc = new DateTime(2026, 1, 2) };

        // Act
        var decision = await service.SelectAgentAsync(
            queue,
            item,
            [Availability(stickyAgent), Availability(otherAgent)],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.Succeeded);
        Assert.Same(otherAgent, decision.Agent);
    }

    [Fact]
    public async Task SelectAgentAsync_WhenEveryAgentHasDeclined_StartsAnotherRoundWithTheEarliestDecliner()
    {
        // Arrange
        // a2 declined first and a3 has been idle longest, but a1 declined before either of them: a new round goes in
        // the order the first one did, and never straight back to a3, who declined last.
        var service = CreateService();
        var queue = new ActivityQueue { ItemId = "q1" };
        var item = new QueueItem { ItemId = "i1", QueueId = "q1", DeclinedAgentIds = ["a1", "a2", "a3"], LastDeclinedUtc = _now };
        var first = new AgentProfile { ItemId = "a1", UserId = "u1", IdleSinceUtc = new DateTime(2026, 1, 3) };
        var second = new AgentProfile { ItemId = "a2", UserId = "u2", IdleSinceUtc = new DateTime(2026, 1, 2) };
        var last = new AgentProfile { ItemId = "a3", UserId = "u3", IdleSinceUtc = new DateTime(2026, 1, 1) };

        // Act
        var decision = await service.SelectAgentAsync(
            queue,
            item,
            [Availability(last), Availability(second), Availability(first)],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.Succeeded);
        Assert.Same(first, decision.Agent);
    }

    [Fact]
    public async Task SelectAgentAsync_TheOnlyAgent_WhoJustDeclined_IsOfferedItAgainStraightAway()
    {
        // Arrange
        // Nobody else can take the call, so it is better rung again than left on hold with an agent free.
        var service = new ActivityRoutingService([new LongestIdleRoutingStrategy()]);
        var queue = new ActivityQueue { ItemId = "q1" };
        var item = new QueueItem { ItemId = "i1", QueueId = "q1", DeclinedAgentIds = ["a1"], LastDeclinedUtc = _now };
        var onlyAgent = new AgentProfile { ItemId = "a1", UserId = "u1" };

        // Act
        var decision = await service.SelectAgentAsync(queue, item, [Availability(onlyAgent)], TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.Succeeded);
        Assert.Same(onlyAgent, decision.Agent);
    }

    [Fact]
    public async Task SelectAgentAsync_AnAgentOnAnotherCall_DoesNotHoldTheCallBackFromTheDecliner()
    {
        // Arrange
        // A round is judged against who can take the call now. The other agent is at capacity, so the decliner is the
        // only one who can, and is offered it again rather than the caller waiting for the other call to end.
        var service = CreateServiceWithCapacity();
        var queue = new ActivityQueue { ItemId = "q1" };
        var item = new QueueItem { ItemId = "i1", QueueId = "q1", DeclinedAgentIds = ["a1"], LastDeclinedUtc = _now };
        var decliner = new AgentProfile { ItemId = "a1", UserId = "u1", MaxConcurrentInteractions = 1 };
        var busyAgent = new AgentProfile { ItemId = "a2", UserId = "u2", MaxConcurrentInteractions = 1 };

        // Act
        var decision = await service.SelectAgentAsync(
            queue,
            item,
            [Availability(decliner), Availability(busyAgent, activeInteractions: 1)],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.Succeeded);
        Assert.Same(decliner, decision.Agent);
    }

    private static readonly DateTime _now = new(2026, 9, 25, 20, 11, 27, DateTimeKind.Utc);

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
