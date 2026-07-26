using CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Verifies the guard that rejects a shared aggregate health endpoint named as a liveness probe.
/// </summary>
public sealed class SharedHealthCheckEndpointGuardTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/health/live")]
    [InlineData("health/live")]
    [InlineData("/health/live/")]
    [InlineData("/HEALTH/LIVE")]
    [InlineData("/health/liveness")]
    [InlineData("/live")]
    public void IsUnsafeRoute_ReturnsTrue_WhenTheRouteClaimsLiveness(string configuredRoute)
    {
        Assert.True(SharedHealthCheckEndpointGuard.IsUnsafeRoute(configuredRoute));
    }

    [Theory]
    [InlineData("/health/aggregate")]
    [InlineData("/health/ready")]
    [InlineData("/health")]
    [InlineData("/health/all-modules")]
    [InlineData("/health/liveliness-report")]
    public void IsUnsafeRoute_ReturnsFalse_WhenTheRouteDoesNotClaimLiveness(string configuredRoute)
    {
        Assert.False(SharedHealthCheckEndpointGuard.IsUnsafeRoute(configuredRoute));
    }

    [Fact]
    public void Validate_Throws_WhenTheRouteIsLeftAtTheUnsafeDefault()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => SharedHealthCheckEndpointGuard.Validate(configuredRoute: null, acknowledged: false));

        // The message has to be actionable on its own, because it surfaces during shell startup where the
        // operator has no other context.
        Assert.Contains(SharedHealthCheckEndpointGuard.DefaultSharedEndpointRoute, exception.Message, StringComparison.Ordinal);
        Assert.Contains("OrchardCore_HealthChecks:Url", exception.Message, StringComparison.Ordinal);
        Assert.Contains("AllowUnsafeSharedEndpointRoute", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_DoesNotThrow_WhenTheOperatorAcknowledgedTheRoute()
    {
        SharedHealthCheckEndpointGuard.Validate(configuredRoute: null, acknowledged: true);
    }

    [Fact]
    public void Validate_DoesNotThrow_WhenTheRouteWasMovedOffALivenessName()
    {
        SharedHealthCheckEndpointGuard.Validate("/health/aggregate", acknowledged: false);
    }
}
