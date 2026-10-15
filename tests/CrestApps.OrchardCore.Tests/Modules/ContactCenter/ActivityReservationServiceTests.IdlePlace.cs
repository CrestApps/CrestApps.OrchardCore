#nullable enable annotations

using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Longest-idle routing ranks agents by <see cref="AgentProfile.IdleSinceUtc"/>, and nothing routing did to the agent
/// used to touch it, so an agent returned from a reservation or a call kept the stamp from before it and stayed first in
/// line. These prove each of routing's own transitions leaves the stamp where the fairness rule says.
/// </summary>
public sealed partial class ActivityReservationServiceTests
{
    private static readonly DateTime _idleSince = new(2025, 12, 31, 23, 50, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ReserveAsync_HoldsTheAgentsPlaceInTheIdleLine()
    {
        // Arrange
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager
            .Setup(m => m.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityReservation { ItemId = "r1" });
        var queueItemManager = new Mock<IQueueItemManager>();
        var item = new QueueItem { ItemId = "qi-1", QueueId = "q1", ActivityItemId = "act-1" };
        queueItemManager.Setup(m => m.FindByIdAsync("qi-1", It.IsAny<CancellationToken>())).ReturnsAsync(item);
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1", PresenceStatus = AgentPresenceStatus.Available, IdleSinceUtc = _idleSince };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var service = CreateAuditedService(reservationManager, queueItemManager, agentManager, CreateInteractionManager("act-1", "int-1"), new RecordingAuditRecorder());

        // Act
        await service.ReserveAsync(item, agent, 30, TestContext.Current.CancellationToken);

        // Assert - held, not cleared, so an offer that never reaches the agent can give it back.
        Assert.Equal(AgentPresenceStatus.Reserved, agent.PresenceStatus);
        Assert.Equal(_idleSince, agent.IdleSinceUtc);
    }

    [Fact]
    public async Task ExpireDueAsync_SendsAnAgentWhoLetTheOfferRingOutToTheBackOfTheLine()
    {
        // Arrange
        var reservation = new ActivityReservation { ItemId = "r1", QueueItemId = "qi-1", AgentId = "a1", ActivityItemId = "act-1" }
            .RestorePersistedStatus(ReservationStatus.Pending);
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager
            .Setup(m => m.GetExpiredAsync(_now, It.IsAny<DateTime?>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExpiredReservationPage([reservation], null, 0));
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
        var agent = ReservedAgent();
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var service = CreateAuditedService(reservationManager, new Mock<IQueueItemManager>(), agentManager, CreateInteractionManager("act-1", "int-1"), new RecordingAuditRecorder());

        // Act
        await service.ExpireDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.Available, agent.PresenceStatus);
        Assert.Equal(_now, agent.IdleSinceUtc);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CancelOrRejectAsync_KeepsThePlaceOnlyWhenTheCallerWentAway(bool reject)
    {
        // Arrange
        var reservation = new ActivityReservation { ItemId = "r1", QueueItemId = "qi-1", AgentId = "a1", ActivityItemId = "act-1" }
            .RestorePersistedStatus(ReservationStatus.Pending);
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
        var agent = ReservedAgent();
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var service = CreateAuditedService(reservationManager, new Mock<IQueueItemManager>(), agentManager, CreateInteractionManager("act-1", "int-1"), new RecordingAuditRecorder());

        // Act
        if (reject)
        {
            await service.RejectAsync("r1", TestContext.Current.CancellationToken);
        }
        else
        {
            await service.CancelAsync("r1", TestContext.Current.CancellationToken);
        }

        // Assert - declining reached the agent, so they go behind the colleagues who waited while it rang; a caller who
        // hung up while it rang never reached them, so they keep their place.
        Assert.Equal(AgentPresenceStatus.Available, agent.PresenceStatus);
        Assert.Equal(reject ? _now : _idleSince, agent.IdleSinceUtc);
    }

    [Fact]
    public async Task AcceptAsync_HoldsThePlace_AndCompensatingASuppressedDialGivesItBack()
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
        await service.AcceptAsync("r1", TestContext.Current.CancellationToken);
        var heldWhileBusy = agent.IdleSinceUtc;
        await service.CompensateAsync("r1", removeFromQueue: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_idleSince, heldWhileBusy);
        Assert.Equal(AgentPresenceStatus.Available, agent.PresenceStatus);
        Assert.Equal(_idleSince, agent.IdleSinceUtc);
    }

    private static AgentProfile ReservedAgent()
        => new()
        {
            ItemId = "a1",
            UserId = "u1",
            PresenceStatus = AgentPresenceStatus.Reserved,
            RequestedPresenceStatus = AgentPresenceStatus.Available,
            ActiveReservationId = "r1",
            IdleSinceUtc = _idleSince,
        };
}
