using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class AgentAvailabilityServiceTests
{
    private static readonly DateTime _now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ListForQueueAsync_WhenProfileHasNoSession_ReturnsNoAvailability()
    {
        // Arrange
        var agent = CreateAgent();
        var service = CreateService([agent], []);

        // Act
        var availability = await service.ListForQueueAsync("q1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(availability);
    }

    [Fact]
    public async Task GetAsync_WhenLastConnectionDisconnected_ReturnsNoAvailability()
    {
        // Arrange
        var agent = CreateAgent();
        agent.AllowedQueueIds = ["q1"];
        agent.QueueIds = ["q1"];
        var session = CreateSession(isOnline: false, connectionCount: 0, _now);
        var service = CreateService([agent], [session]);

        // Act
        var availability = await service.GetAsync("a1", "q1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(availability);
    }

    [Fact]
    public async Task GetAsync_WhenHeartbeatIsRefreshed_ReturnsAvailability()
    {
        // Arrange
        var agent = CreateAgent();
        agent.AllowedQueueIds = ["q1"];
        agent.QueueIds = ["q1"];
        var session = CreateSession(isOnline: true, connectionCount: 1, _now);
        var service = CreateService([agent], [session]);

        // Act
        var availability = await service.GetAsync("a1", "q1", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(availability);
        Assert.Equal(_now, availability.LastHeartbeatUtc);
    }

    [Theory]
    [InlineData(AgentPresenceStatus.Busy)]
    [InlineData(AgentPresenceStatus.WrapUp)]
    public async Task GetAsync_WhenAgentIsNotAvailable_ReturnsNoAvailability(AgentPresenceStatus presenceStatus)
    {
        // Arrange
        var agent = CreateAgent();
        agent.AllowedQueueIds = ["q1"];
        agent.QueueIds = ["q1"];
        agent.PresenceStatus = presenceStatus;
        var session = CreateSession(isOnline: true, connectionCount: 1, _now);
        var service = CreateService([agent], [session]);

        // Act
        var availability = await service.GetAsync("a1", "q1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(availability);
    }

    [Fact]
    public async Task GetAsync_WhenAgentLacksQueueEntitlement_ReturnsNoAvailability()
    {
        // Arrange
        var agent = CreateAgent();
        agent.QueueIds = ["q1"];
        var session = CreateSession(isOnline: true, connectionCount: 1, _now);
        var service = CreateService([agent], [session]);

        // Act
        var availability = await service.GetAsync("a1", "q1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(availability);
    }

    [Theory]
    [InlineData(91, "q1", 0)]
    [InlineData(0, "q2", 0)]
    [InlineData(0, "q1", 1)]
    public async Task GetAsync_WhenCanonicalRequirementFails_ReturnsNoAvailability(
        int heartbeatAgeSeconds,
        string sessionQueueId,
        int activeInteractionCount)
    {
        // Arrange
        var agent = CreateAgent();
        agent.AllowedQueueIds = ["q1"];
        agent.QueueIds = ["q1"];
        var session = CreateSession(
            isOnline: true,
            connectionCount: 1,
            _now.AddSeconds(-heartbeatAgeSeconds));
        session.QueueIds = [sessionQueueId];
        var service = CreateService(
            [agent],
            [session],
            new Dictionary<string, int> { ["a1"] = activeInteractionCount });

        // Act
        var availability = await service.GetAsync("a1", "q1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(availability);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    public async Task ListForQueueAsync_WhenSessionIsOfflineOrDisconnected_ReturnsNoAvailability(
        bool isOnline,
        int connectionCount)
    {
        // Arrange
        var agent = CreateAgent();
        var session = CreateSession(isOnline, connectionCount, _now);
        var service = CreateService([agent], [session]);

        // Act
        var availability = await service.ListForQueueAsync("q1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(availability);
    }

    [Fact]
    public async Task ListForQueueAsync_WhenHeartbeatIsStale_ReturnsNoAvailability()
    {
        // Arrange
        var agent = CreateAgent();
        var session = CreateSession(isOnline: true, connectionCount: 1, _now.AddSeconds(-91));
        var service = CreateService([agent], [session]);

        // Act
        var availability = await service.ListForQueueAsync("q1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(availability);
    }

    [Fact]
    public async Task ListForQueueAsync_WhenSessionIsLiveAndAgentHasCapacity_ReturnsCanonicalAvailability()
    {
        // Arrange
        var agent = CreateAgent();
        agent.MaxConcurrentInteractions = 2;
        var session = CreateSession(isOnline: true, connectionCount: 1, _now);
        var service = CreateService([agent], [session], new Dictionary<string, int> { ["a1"] = 1 });

        // Act
        var availability = await service.ListForQueueAsync("q1", TestContext.Current.CancellationToken);

        // Assert
        var result = Assert.Single(availability);
        Assert.Same(agent, result.Agent);
        Assert.Equal(_now, result.LastHeartbeatUtc);
        Assert.Equal(1, result.ActiveInteractionCount);
        Assert.Equal(1, result.RemainingCapacity);
    }

    [Fact]
    public async Task ListForQueueAsync_WhenAgentHasNoRemainingCapacity_ReturnsNoAvailability()
    {
        // Arrange
        var agent = CreateAgent();
        var session = CreateSession(isOnline: true, connectionCount: 1, _now);
        var service = CreateService([agent], [session], new Dictionary<string, int> { ["a1"] = 1 });

        // Act
        var availability = await service.ListForQueueAsync("q1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(availability);
    }

    [Fact]
    public async Task ListForQueueAsync_WhenSessionDidNotOptIntoQueue_ReturnsNoAvailability()
    {
        // Arrange
        var agent = CreateAgent();
        var session = CreateSession(isOnline: true, connectionCount: 1, _now);
        session.QueueIds = ["q2"];
        var service = CreateService([agent], [session]);

        // Act
        var availability = await service.ListForQueueAsync("q1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(availability);
    }

    private static AgentAvailabilityService CreateService(
        IReadOnlyCollection<AgentProfile> agents,
        IReadOnlyCollection<AgentSession> sessions,
        Dictionary<string, int> activeCounts = null)
    {
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.ListAvailableForQueueAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);
        agentManager
            .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string agentId, CancellationToken _) => agents.FirstOrDefault(agent => agent.ItemId == agentId));

        var sessionManager = new Mock<IAgentSessionManager>();
        sessionManager
            .Setup(manager => manager.ListByUserIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);
        sessionManager
            .Setup(manager => manager.FindByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, CancellationToken _) => sessions.FirstOrDefault(session => session.UserId == userId));

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.CountActiveByAgentIdsAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeCounts ?? new Dictionary<string, int>());
        interactionManager
            .Setup(manager => manager.CountActiveByAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string agentId, CancellationToken _) =>
                activeCounts is not null && activeCounts.TryGetValue(agentId, out var count) ? count : 0);

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return new AgentAvailabilityService(
            agentManager.Object,
            sessionManager.Object,
            interactionManager.Object,
            Options.Create(new AgentAvailabilityOptions()),
            clock.Object);
    }

    private static AgentProfile CreateAgent()
    {
        return new AgentProfile
        {
            ItemId = "a1",
            UserId = "u1",
            PresenceStatus = AgentPresenceStatus.Available,
            MaxConcurrentInteractions = 1,
        };
    }

    private static AgentSession CreateSession(bool isOnline, int connectionCount, DateTime heartbeatUtc)
    {
        return new AgentSession
        {
            ItemId = "s1",
            UserId = "u1",
            IsOnline = isOnline,
            ConnectionIds = Enumerable.Range(1, connectionCount).Select(index => $"c{index}").ToList(),
            QueueIds = ["q1"],
            LastHeartbeatUtc = heartbeatUtc,
        };
    }
}
