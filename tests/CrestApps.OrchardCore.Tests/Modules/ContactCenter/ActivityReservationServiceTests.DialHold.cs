#nullable enable annotations

using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// A power dial accepted its reservation, and made the agent Busy -- "On a call" on every screen and in every report --
/// before the customer's phone even rang, so every dial nobody answered counted as a call the agent took. An automated
/// dial now holds the agent Reserved until they are connected, as contact-center dialers do.
/// </summary>
public sealed partial class ActivityReservationServiceTests
{
    [Fact]
    public async Task AcceptForDialAsync_HoldsTheAgentReserved_NotOnACall()
    {
        // Arrange
        var reservation = new ActivityReservation { ItemId = "r1", QueueItemId = "qi-1", AgentId = "a1", ActivityItemId = "act-1" }
            .RestorePersistedStatus(ReservationStatus.Pending);
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1", PresenceStatus = AgentPresenceStatus.Reserved, ActiveReservationId = "r1" };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var recorder = new RecordingAuditRecorder();
        var service = CreateAuditedService(
            reservationManager,
            new Mock<IQueueItemManager>(),
            agentManager,
            CreateInteractionManager("act-1", "int-1"),
            recorder);

        // Act
        var accepted = await service.AcceptForDialAsync("r1", TestContext.Current.CancellationToken);

        // Assert - the work is the agent's, but they are held for the call, not on it, and no call is on record yet.
        Assert.NotNull(accepted);
        Assert.Equal(ReservationStatus.Accepted, reservation.Status);
        Assert.Equal(AgentPresenceStatus.Reserved, agent.PresenceStatus);
        Assert.Equal("r1", agent.DialHoldReservationId);
        Assert.Null(agent.ActiveReservationId);
        Assert.DoesNotContain(recorder.StateChanges, recorded => recorded.Change.CurrentState == AgentPresenceStatus.Busy);
    }

    [Fact]
    public async Task CompensateAsync_ReleasesAnAgentHeldForADialThatWasNeverPlaced()
    {
        // Arrange - a power dial is accepted for the agent before it is placed, then routing takes it back unplaced.
        var reservation = new ActivityReservation { ItemId = "r1", QueueItemId = "qi-1", AgentId = "a1", ActivityItemId = "act-1" }
            .RestorePersistedStatus(ReservationStatus.Pending);
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
        reservationManager.Setup(m => m.GetActiveByAgentAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var agent = ReservedAgent();
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var service = CreateAuditedService(reservationManager, new Mock<IQueueItemManager>(), agentManager, CreateInteractionManager("act-1", "int-1"), new RecordingAuditRecorder());

        // Act
        await service.AcceptForDialAsync("r1", TestContext.Current.CancellationToken);
        await service.CompensateAsync("r1", removeFromQueue: true, TestContext.Current.CancellationToken);

        // Assert - back to ready, keeping their place in line, and nothing left holding them.
        Assert.Equal(AgentPresenceStatus.Available, agent.PresenceStatus);
        Assert.Equal(_idleSince, agent.IdleSinceUtc);
        Assert.Null(agent.DialHoldReservationId);
    }
}
