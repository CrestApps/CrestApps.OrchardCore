using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// An automated dial holds its agent Reserved while the customer is reached, and puts them on a call only once a person
/// answers and they are connected, as contact-center dialers do. Before, the agent was Busy -- "On a call" -- from the
/// dial, so every attempt nobody answered counted as a call and as talk time.
/// </summary>
public sealed class AgentPresenceDialHoldTests
{
    private static readonly DateTime _now = new(2026, 10, 19, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ConnectHeldAgentAsync_PutsTheHeldAgentOnTheCall_AsOfTheConnect()
    {
        // Arrange
        var agent = HeldAgent();
        var recorder = new RecordingAuditRecorder();
        var publisher = new Mock<IContactCenterEventPublisher>();
        var service = CreateService(agent, recorder, publisher);
        var connectedUtc = _now.AddSeconds(-2);

        // Act
        await service.ConnectHeldAgentAsync("a1", new AgentStateChangeContext
        {
            InteractionId = "int-1",
            ReservationId = "r1",
            ChangedUtc = connectedUtc,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.Busy, agent.PresenceStatus);
        Assert.Null(agent.DialHoldReservationId);
        var recorded = Assert.Single(recorder.StateChanges);
        Assert.Equal(AgentPresenceStatus.Reserved, recorded.Change.PreviousState);
        Assert.Equal(AgentPresenceStatus.Busy, recorded.Change.CurrentState);
        Assert.Equal(AgentStateChangeSources.Connected, recorded.Change.Source);
        Assert.Equal("r1", recorded.Change.ReservationId);
        Assert.Equal("int-1", recorded.Change.InteractionId);
        Assert.Equal(connectedUtc, recorded.Change.ChangedUtc);
        publisher.Verify(
            p => p.PublishAsync(It.Is<InteractionEvent>(e => e.EventType == ContactCenterConstants.Events.AgentPresenceChanged), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ConnectHeldAgentAsync_ARepeatedConnect_ChangesNothing()
    {
        // Arrange
        var agent = HeldAgent();
        var recorder = new RecordingAuditRecorder();
        var service = CreateService(agent, recorder);
        var context = new AgentStateChangeContext { InteractionId = "int-1", ReservationId = "r1", ChangedUtc = _now };

        // Act
        await service.ConnectHeldAgentAsync("a1", context, TestContext.Current.CancellationToken);
        await service.ConnectHeldAgentAsync("a1", context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.Busy, agent.PresenceStatus);
        Assert.Single(recorder.StateChanges);
    }

    // A Reserved agent with a ringing offer, or one held for another call, is not the agent this connect is about.
    [Theory]
    [InlineData(null, "r1", "r1")]
    [InlineData("r2", null, "r1")]
    public async Task ConnectHeldAgentAsync_LeavesAnAgentNotHeldForThisCallAlone(string holdReservationId, string activeReservationId, string connectedReservationId)
    {
        // Arrange
        var agent = HeldAgent();
        agent.DialHoldReservationId = holdReservationId;
        agent.ActiveReservationId = activeReservationId;
        var recorder = new RecordingAuditRecorder();
        var service = CreateService(agent, recorder);

        // Act
        await service.ConnectHeldAgentAsync("a1", new AgentStateChangeContext { ReservationId = connectedReservationId }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.Reserved, agent.PresenceStatus);
        Assert.Equal(holdReservationId, agent.DialHoldReservationId);
        Assert.Empty(recorder.StateChanges);
    }

    // No offer rings for a held agent, which used to read as "stranded in Reserved" and apply a requested state at
    // once: an agent who picked Away mid-dial would have been Away when the customer answered.
    [Fact]
    public async Task SetPresenceAsync_WhileADialHoldsTheAgent_WaitsForTheCall()
    {
        // Arrange
        var agent = HeldAgent();
        var service = CreateService(agent, new RecordingAuditRecorder());

        // Act
        var profile = await service.SetPresenceAsync("u1", AgentPresenceStatus.Away, "Lunch", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.Reserved, profile.PresenceStatus);
        Assert.Equal(AgentPresenceStatus.Away, profile.RequestedPresenceStatus);
        Assert.Equal("r1", profile.DialHoldReservationId);
    }

    [Fact]
    public async Task TransitionAsync_LeavingReserved_EndsTheDialHold()
    {
        // Arrange
        var agent = HeldAgent();
        var transitions = AgentStateAuditTestDoubles.CreateTransitions(new RecordingAuditRecorder());

        // Act
        await transitions.TransitionAsync(agent, AgentPresenceStatus.Available, new AgentStateChangeContext { KeepsIdlePlace = true }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(agent.DialHoldReservationId);
    }

    private static AgentProfile HeldAgent()
        => new()
        {
            ItemId = "a1",
            UserId = "u1",
            PresenceStatus = AgentPresenceStatus.Reserved,
            PresenceChangedUtc = _now.AddSeconds(-20),
            DialHoldReservationId = "r1",
        };

    private static AgentPresenceManagerService CreateService(
        AgentProfile agent,
        RecordingAuditRecorder recorder,
        Mock<IContactCenterEventPublisher> publisher = null)
    {
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        agentManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((null, true));

        return new AgentPresenceManagerService(
            agentManager.Object,
            [],
            new NoAgentWorkStateHealingService(),
            new EnforcingAgentEntitlementPolicy(),
            AgentStateAuditTestDoubles.CreateTransitions(recorder, clock.Object),
            (publisher ?? new Mock<IContactCenterEventPublisher>()).Object,
            distributedLock.Object,
            clock.Object,
            new Mock<ILogger<AgentPresenceManagerService>>().Object);
    }
}
