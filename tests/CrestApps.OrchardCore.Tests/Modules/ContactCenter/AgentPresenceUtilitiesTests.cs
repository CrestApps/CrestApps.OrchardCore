using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Proves the default ready-state resolver keeps a signed-out agent offline while returning any other agent to
/// available, so ending a call or releasing a stale reservation never signs an agent out and never revives one
/// who genuinely signed out; and that the idle stamp longest-idle routing ranks by follows every transition.
/// </summary>
public sealed class AgentPresenceUtilitiesTests
{
    [Fact]
    public void ResolveDefaultReadyState_WhenAgentIsOffline_StaysOffline()
    {
        // Arrange
        var profile = new AgentProfile { PresenceStatus = AgentPresenceStatus.Offline };

        // Act
        var resolved = AgentPresenceUtilities.ResolveDefaultReadyState(profile);

        // Assert - a deliberate sign-out is never undone by a reservation releasing.
        Assert.Equal(AgentPresenceStatus.Offline, resolved);
    }

    [Theory]
    [InlineData(AgentPresenceStatus.Available)]
    [InlineData(AgentPresenceStatus.Busy)]
    [InlineData(AgentPresenceStatus.Away)]
    public void ResolveDefaultReadyState_WhenAgentIsNotOffline_ReturnsAvailable(AgentPresenceStatus status)
    {
        // Arrange - availability is reachability, not queue eligibility, so a working agent returns to available.
        var profile = new AgentProfile { PresenceStatus = status };

        // Act
        var resolved = AgentPresenceUtilities.ResolveDefaultReadyState(profile);

        // Assert
        Assert.Equal(AgentPresenceStatus.Available, resolved);
    }

    [Fact]
    public void ResolveDefaultReadyState_WithNullProfile_Throws()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() => AgentPresenceUtilities.ResolveDefaultReadyState(null));
    }

    private static readonly DateTime _heldSince = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _changed = new(2026, 1, 1, 9, 30, 0, DateTimeKind.Utc);

    // Every way back to Available from an offer or a call, unless the work never reached the agent, starts a fresh idle
    // clock. Before, a return that skipped wrap-up kept the stamp from before the work, and that agent stayed first in
    // line for call after call.
    [Theory]
    [InlineData(AgentPresenceStatus.Reserved, false, false)]
    [InlineData(AgentPresenceStatus.Busy, false, false)]
    [InlineData(AgentPresenceStatus.Reserved, true, true)]
    [InlineData(AgentPresenceStatus.Busy, true, true)]
    [InlineData(AgentPresenceStatus.Available, false, true)]
    [InlineData(AgentPresenceStatus.WrapUp, false, false)]
    [InlineData(AgentPresenceStatus.Break, true, false)]
    [InlineData(AgentPresenceStatus.Offline, true, false)]
    public void ApplyIdleState_ReturningToAvailable_KeepsThePlaceOnlyWhenNothingReachedTheAgent(AgentPresenceStatus previous, bool keepsIdlePlace, bool keepsPlace)
    {
        // Arrange
        var profile = new AgentProfile { PresenceStatus = AgentPresenceStatus.Available, IdleSinceUtc = _heldSince };

        // Act
        AgentPresenceUtilities.ApplyIdleState(profile, previous, _changed, keepsIdlePlace);

        // Assert
        Assert.Equal(keepsPlace ? _heldSince : _changed, profile.IdleSinceUtc);
    }

    [Theory]
    [InlineData(AgentPresenceStatus.Available, AgentPresenceStatus.Reserved)]
    [InlineData(AgentPresenceStatus.Available, AgentPresenceStatus.Busy)]
    [InlineData(AgentPresenceStatus.Reserved, AgentPresenceStatus.Busy)]
    [InlineData(AgentPresenceStatus.Busy, AgentPresenceStatus.Busy)]
    public void ApplyIdleState_IntoAnOfferOrACall_HoldsTheStamp(AgentPresenceStatus previous, AgentPresenceStatus current)
    {
        // Arrange
        var profile = new AgentProfile { PresenceStatus = current, ActiveReservationId = "r1", IdleSinceUtc = _heldSince };

        // Act
        AgentPresenceUtilities.ApplyIdleState(profile, previous, _changed, keepsIdlePlace: false);

        // Assert
        Assert.Equal(_heldSince, profile.IdleSinceUtc);
    }

    [Theory]
    [InlineData(AgentPresenceStatus.Busy, AgentPresenceStatus.WrapUp)]
    [InlineData(AgentPresenceStatus.Available, AgentPresenceStatus.Break)]
    [InlineData(AgentPresenceStatus.Busy, AgentPresenceStatus.Offline)]
    [InlineData(AgentPresenceStatus.Break, AgentPresenceStatus.Busy)]
    public void ApplyIdleState_IntoAnyOtherState_ClearsTheStamp(AgentPresenceStatus previous, AgentPresenceStatus current)
    {
        // Arrange
        var profile = new AgentProfile { PresenceStatus = current, IdleSinceUtc = _heldSince };

        // Act
        AgentPresenceUtilities.ApplyIdleState(profile, previous, _changed, keepsIdlePlace: true);

        // Assert
        Assert.Null(profile.IdleSinceUtc);
    }

    [Fact]
    public void ApplyIdleState_KeepingAPlaceThatWasNeverHeld_StartsTheClockNow()
    {
        // Arrange
        var profile = new AgentProfile { PresenceStatus = AgentPresenceStatus.Available };

        // Act
        AgentPresenceUtilities.ApplyIdleState(profile, AgentPresenceStatus.Busy, _changed, keepsIdlePlace: true);

        // Assert
        Assert.Equal(_changed, profile.IdleSinceUtc);
    }
}
