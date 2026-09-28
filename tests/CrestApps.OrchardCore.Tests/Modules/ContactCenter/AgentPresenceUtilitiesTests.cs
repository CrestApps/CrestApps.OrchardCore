using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Proves the default ready-state resolver keeps a signed-out agent offline while returning any other agent to
/// available, so ending a call or releasing a stale reservation never signs an agent out and never revives one
/// who genuinely signed out.
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
}
