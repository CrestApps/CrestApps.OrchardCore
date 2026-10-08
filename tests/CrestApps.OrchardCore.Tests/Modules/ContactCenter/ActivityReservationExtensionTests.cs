using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Reviewing a record before dialing sometimes takes longer than the offer allows. Losing the offer throws the
/// review work away and returns the record to the queue, so the agent can ask for more time -- but only while
/// the offer is still theirs, and only so many times, or one agent could hold a record out of the queue
/// indefinitely.
/// </summary>
public sealed class ActivityReservationExtensionTests
{
    private static readonly DateTime _now = new(2026, 8, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Extend_WhenTheOfferIsStillPending_AddsTheExtensionToTheExistingExpiry()
    {
        // Arrange
        var reservation = CreateReservation(_now.AddSeconds(10));

        // Act
        var extended = reservation.Extend(TimeSpan.FromMinutes(2), maximumExtensions: 2, _now);

        // Assert
        Assert.True(extended);
        Assert.Equal(_now.AddSeconds(10).AddMinutes(2), reservation.ExpiresUtc);
        Assert.Equal(1, reservation.ExtensionCount);
    }

    [Fact]
    public void Extend_WhenTheOfferHasJustLapsed_GrantsTheFullExtensionFromNow()
    {
        // Arrange
        // Measuring from a stale expiry would hand back a fraction of the extension -- or none at all -- which
        // is the opposite of what asking for more time is for.
        var reservation = CreateReservation(_now.AddSeconds(-30));

        // Act
        var extended = reservation.Extend(TimeSpan.FromMinutes(2), maximumExtensions: 2, _now);

        // Assert
        Assert.True(extended);
        Assert.Equal(_now.AddMinutes(2), reservation.ExpiresUtc);
    }

    [Fact]
    public void Extend_WhenTheCapIsReached_RefusesSoOneAgentCannotHoldTheRecord()
    {
        // Arrange
        var reservation = CreateReservation(_now.AddSeconds(10));

        Assert.True(reservation.Extend(TimeSpan.FromMinutes(2), maximumExtensions: 2, _now));
        Assert.True(reservation.Extend(TimeSpan.FromMinutes(2), maximumExtensions: 2, _now));

        var expiryAtCap = reservation.ExpiresUtc;

        // Act
        var extended = reservation.Extend(TimeSpan.FromMinutes(2), maximumExtensions: 2, _now);

        // Assert
        Assert.False(extended);
        Assert.Equal(expiryAtCap, reservation.ExpiresUtc);
        Assert.Equal(2, reservation.ExtensionCount);
    }

    [Fact]
    public void Extend_WhenTheOfferIsAlreadyResolved_RefusesBecauseItIsNoLongerTheAgentsToHold()
    {
        // Arrange
        var reservation = CreateReservation(_now.AddSeconds(10));
        reservation.TransitionTo(ReservationStatus.Accepted);

        var expiry = reservation.ExpiresUtc;

        // Act
        var extended = reservation.Extend(TimeSpan.FromMinutes(2), maximumExtensions: 2, _now);

        // Assert
        Assert.False(extended);
        Assert.Equal(expiry, reservation.ExpiresUtc);
        Assert.Equal(0, reservation.ExtensionCount);
    }

    private static ActivityReservation CreateReservation(DateTime expiresUtc)
        => new()
        {
            ItemId = "reservation-1",
            ActivityItemId = "activity-1",
            AgentId = "agent-1",
            CreatedUtc = _now,
            ExpiresUtc = expiresUtc,
        };
}
