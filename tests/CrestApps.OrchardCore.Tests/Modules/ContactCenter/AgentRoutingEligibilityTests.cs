using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class AgentRoutingEligibilityTests
{
    private static readonly DateTime _now = new(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _liveAfter = _now.AddSeconds(-90);

    [Fact]
    public void Evaluate_WhenEveryRuleHolds_ReturnsNone()
    {
        // Arrange
        var agent = CreateAgent();
        var session = CreateSession();

        // Act
        var result = AgentRoutingEligibility.Evaluate(agent, session, "q1", activeInteractionCount: 0, _liveAfter);

        // Assert
        Assert.Equal(AgentRoutingIneligibility.None, result);
    }

    [Fact]
    public void Evaluate_WhenEveryRuleFails_ReturnsEveryReason()
    {
        // Arrange
        var agent = CreateAgent();
        agent.PresenceStatus = AgentPresenceStatus.Break;
        agent.QueueIds = [];
        agent.AllowedQueueIds = [];
        var session = CreateSession();
        session.LastHeartbeatUtc = _now.AddMinutes(-10);
        session.QueueIds = [];

        // Act
        var result = AgentRoutingEligibility.Evaluate(agent, session, "q1", activeInteractionCount: 1, _liveAfter);

        // Assert
        Assert.Equal(
            AgentRoutingIneligibility.NotAvailable |
            AgentRoutingIneligibility.NotEntitled |
            AgentRoutingIneligibility.NotSignedIn |
            AgentRoutingIneligibility.NoLiveSession |
            AgentRoutingIneligibility.SessionNotSignedIn |
            AgentRoutingIneligibility.AtCapacity,
            result);
    }

    [Fact]
    public void GetSessionIneligibility_WhenThereIsNoSession_ReturnsOnlyNoLiveSession()
    {
        // Act
        var result = AgentRoutingEligibility.GetSessionIneligibility(null, "q1", _liveAfter);

        // Assert
        Assert.Equal(AgentRoutingIneligibility.NoLiveSession, result);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public void IsSessionLive_WhenOfflineOrDisconnected_ReturnsFalse(bool isOnline, int connectionCount)
    {
        // Arrange
        var session = CreateSession();
        session.IsOnline = isOnline;
        session.ConnectionIds = Enumerable.Range(0, connectionCount).Select(index => $"c{index}").ToList();

        // Act
        var live = AgentRoutingEligibility.IsSessionLive(session, _liveAfter);

        // Assert
        Assert.False(live);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0, 1, true)]
    [InlineData(2, 1, false)]
    [InlineData(2, 2, true)]
    public void IsAtCapacity_TreatsNoCapacityAsOne(int maxConcurrentInteractions, int activeInteractionCount, bool expected)
    {
        // Arrange
        var agent = CreateAgent();
        agent.MaxConcurrentInteractions = maxConcurrentInteractions;

        // Act
        var atCapacity = AgentRoutingEligibility.IsAtCapacity(agent, activeInteractionCount);

        // Assert
        Assert.Equal(expected, atCapacity);
    }

    private static AgentProfile CreateAgent()
        => new()
        {
            ItemId = "a1",
            UserId = "u1",
            PresenceStatus = AgentPresenceStatus.Available,
            QueueIds = ["q1"],
            AllowedQueueIds = ["Q1"],
            MaxConcurrentInteractions = 1,
        };

    private static AgentSession CreateSession()
        => new()
        {
            UserId = "u1",
            IsOnline = true,
            ConnectionIds = ["c1"],
            LastHeartbeatUtc = _now,
            QueueIds = ["q1"],
        };
}
