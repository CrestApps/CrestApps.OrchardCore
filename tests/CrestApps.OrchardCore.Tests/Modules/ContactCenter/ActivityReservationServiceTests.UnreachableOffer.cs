#nullable enable annotations

using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Releasing an offer still ringing when the agent's phone refused the leg rung for it as unavailable: the caller is
/// handed on at once, as an offer that rang out is, and the agent is set Away.
/// </summary>
public sealed partial class ActivityReservationServiceTests
{
    [Fact]
    public async Task ReleaseUnreachableOfferAsync_WhenTheOfferIsStillRinging_RingsItOutNowAndSetsTheAgentAway()
    {
        // Arrange
        var fixture = RingingOffer();
        var service = fixture.CreateService();

        // Act
        var released = await service.ReleaseUnreachableOfferAsync("r1", "a1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(fixture.Reservation, released);
        Assert.Equal(ReservationStatus.Expired, fixture.Reservation.Status);

        // The caller waits for the next agent, and this agent's miss puts them behind anyone who has not had the call.
        Assert.Equal(QueueItemStatus.Waiting, fixture.QueueItem.Status);
        Assert.Null(fixture.QueueItem.ReservationId);
        Assert.Null(fixture.QueueItem.AgentId);
        Assert.Contains("a1", fixture.QueueItem.DeclinedAgentIds);
        Assert.Equal(ActivityAssignmentStatus.Available, fixture.Activity.AssignmentStatus);

        // The phone that refused the leg is not offered the next caller.
        Assert.Equal(AgentPresenceStatus.Away, fixture.Agent.PresenceStatus);
        Assert.Equal(AgentPresenceReasons.PhoneUnreachable, fixture.Agent.PresenceReason);
        Assert.Null(fixture.Agent.ActiveReservationId);
        Assert.Contains(fixture.PublishedEvents, e => e.EventType == ContactCenterConstants.Events.AgentPresenceChanged);

        // Settled as an offer that rang out back into its queue, which is what hands the caller to the next agent.
        var offer = Assert.Single(fixture.AuditRecorder.Offers);
        Assert.Equal(ContactCenterConstants.Events.OfferExpired, offer.EventType);
        Assert.Equal(nameof(UnansweredOfferAction.Requeue), offer.Data.Reason);
        fixture.Session.Verify(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(ReservationStatus.Accepted)]
    [InlineData(ReservationStatus.Expired)]
    [InlineData(ReservationStatus.Rejected)]
    public async Task ReleaseUnreachableOfferAsync_WhenTheOfferIsNoLongerRinging_DoesNothing(ReservationStatus status)
    {
        // Arrange
        var fixture = RingingOffer();
        fixture.Reservation.RestorePersistedStatus(status);
        var service = fixture.CreateService();

        // Act
        var released = await service.ReleaseUnreachableOfferAsync("r1", "a1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(released);
        Assert.Equal(status, fixture.Reservation.Status);
        Assert.Equal(AgentPresenceStatus.Reserved, fixture.Agent.PresenceStatus);
        Assert.Empty(fixture.PublishedEvents);
        fixture.Session.Verify(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReleaseUnreachableOfferAsync_WhenTheOfferIsNowAnotherAgents_DoesNothing()
    {
        // Arrange
        var fixture = RingingOffer();
        var service = fixture.CreateService();

        // Act
        var released = await service.ReleaseUnreachableOfferAsync("r1", "a2", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(released);
        Assert.Equal(ReservationStatus.Pending, fixture.Reservation.Status);
        Assert.Empty(fixture.PublishedEvents);
    }

    [Theory]
    [InlineData(ContactCenterConstants.DirectRouting.QueueId)]
    [InlineData("__campaign-queue__campaign-1")]
    public async Task ReleaseUnreachableOfferAsync_WhenTheOfferIsOnAPersonalLineOrACampaignQueue_LeavesItToRingOut(string queueId)
    {
        // Arrange
        var fixture = RingingOffer(queueId);
        var service = fixture.CreateService();

        // Act
        var released = await service.ReleaseUnreachableOfferAsync("r1", "a1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(released);
        Assert.Equal(ReservationStatus.Pending, fixture.Reservation.Status);
        Assert.Empty(fixture.PublishedEvents);
    }

    /// <summary>
    /// The accepted fixture turned back into an offer still ringing: the reservation pending, the agent reserved for
    /// it, and the activity reserved rather than assigned.
    /// </summary>
    private static UnreachableAgentFixture RingingOffer(string queueId = "q1")
    {
        var fixture = new UnreachableAgentFixture(UnansweredOfferAction.Requeue, queueId);
        fixture.Reservation.RestorePersistedStatus(ReservationStatus.Pending);
        fixture.Reservation.ExpiresUtc = _now.AddSeconds(10);
        fixture.QueueItem.RestorePersistedStatus(QueueItemStatus.Reserved);
        fixture.Agent.PresenceStatus = AgentPresenceStatus.Reserved;
        fixture.Agent.ActiveReservationId = "r1";
        fixture.Activity.AssignmentStatus = ActivityAssignmentStatus.Reserved;
        fixture.Activity.AssignedToId = null;
        fixture.Activity.AssignedToUsername = null;
        fixture.Activity.AssignedToUtc = null;

        return fixture;
    }
}
