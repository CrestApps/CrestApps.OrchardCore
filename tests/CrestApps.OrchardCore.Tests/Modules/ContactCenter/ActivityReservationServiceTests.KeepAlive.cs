#nullable enable annotations

using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// A preview offer ran out while the agent reviewed the record and chose a disposition, the record went to someone
/// else, and the agent was refused the disposition. The open activity screen now keeps the offer with them.
/// </summary>
public sealed partial class ActivityReservationServiceTests
{
    [Fact]
    public async Task ExtendPendingAsync_WhenTheAgentStillHoldsTheOffer_PushesItsDeadlineOut()
    {
        // Arrange
        var harness = KeepAliveHarness.Create(ReservationStatus.Pending, agentUserId: "u1", expiresUtc: _now.AddSeconds(10));

        // Act
        var expiresUtc = await harness.Service.ExtendPendingAsync("act-1", "u1", 180, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_now.AddSeconds(180), expiresUtc);
        Assert.Equal(_now.AddSeconds(180), harness.Reservation.ExpiresUtc);
    }

    [Fact]
    public async Task ExtendPendingAsync_WhenAnotherAgentHoldsTheOffer_LeavesItAlone()
    {
        // Arrange
        var harness = KeepAliveHarness.Create(ReservationStatus.Pending, agentUserId: "someone-else", expiresUtc: _now.AddSeconds(10));

        // Act
        var expiresUtc = await harness.Service.ExtendPendingAsync("act-1", "u1", 180, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(expiresUtc);
        Assert.Equal(_now.AddSeconds(10), harness.Reservation.ExpiresUtc);
    }

    // An accepted offer is the agent's assignment and does not run out; only a pending one is extended.
    [Fact]
    public async Task ExtendPendingAsync_WhenTheOfferWasAlreadyAccepted_LeavesItAlone()
    {
        // Arrange
        var harness = KeepAliveHarness.Create(ReservationStatus.Accepted, agentUserId: "u1", expiresUtc: _now.AddSeconds(10));

        // Act
        var expiresUtc = await harness.Service.ExtendPendingAsync("act-1", "u1", 180, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(expiresUtc);
        Assert.Equal(_now.AddSeconds(10), harness.Reservation.ExpiresUtc);
    }

    // Keeping an offer alive never shortens it.
    [Fact]
    public async Task ExtendPendingAsync_WhenTheOfferAlreadyLastsLonger_KeepsItsDeadline()
    {
        // Arrange
        var harness = KeepAliveHarness.Create(ReservationStatus.Pending, agentUserId: "u1", expiresUtc: _now.AddSeconds(600));

        // Act
        var expiresUtc = await harness.Service.ExtendPendingAsync("act-1", "u1", 180, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_now.AddSeconds(600), expiresUtc);
        Assert.Equal(_now.AddSeconds(600), harness.Reservation.ExpiresUtc);
    }

    private sealed class KeepAliveHarness
    {
        public required ActivityReservationService Service { get; init; }

        public required ActivityReservation Reservation { get; init; }

        public static KeepAliveHarness Create(ReservationStatus status, string agentUserId, DateTime expiresUtc)
        {
            var reservation = new ActivityReservation { ItemId = "r1", ActivityItemId = "act-1", AgentId = "a1", ExpiresUtc = expiresUtc };
            reservation.TransitionTo(ReservationStatus.Pending);

            if (status != ReservationStatus.Pending)
            {
                reservation.TransitionTo(status);
            }

            var reservationManager = new Mock<IActivityReservationManager>();
            reservationManager
                .Setup(m => m.GetActiveByActivityAsync("act-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync([reservation]);
            reservationManager
                .Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(reservation);

            var agentManager = new Mock<IAgentProfileManager>();
            agentManager
                .Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentProfile { ItemId = "a1", UserId = agentUserId });

            var service = CreateService(
                reservationManager,
                new Mock<IQueueItemManager>(),
                agentManager,
                new Mock<IActivityQueueManager>(),
                new Mock<IActivityQueueService>(),
                new Mock<IInteractionManager>(),
                new Mock<IOmnichannelActivityManager>(),
                new Mock<IContactCenterEventPublisher>(),
                new Mock<ITelephonyService>());

            return new KeepAliveHarness { Service = service, Reservation = reservation };
        }
    }
}
