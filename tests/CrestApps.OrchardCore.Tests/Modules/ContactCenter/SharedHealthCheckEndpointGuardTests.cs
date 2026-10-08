using CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

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
    public void BuildHazardMessage_ReturnsActionableMessage_WhenTheRouteIsLeftAtTheUnsafeDefault()
    {
        var message = SharedHealthCheckEndpointGuard.BuildHazardMessage(configuredRoute: null, acknowledged: false);

        Assert.NotNull(message);

        // The message has to be actionable on its own, because it surfaces in logs and health data where the
        // operator has no other context.
        Assert.Contains(SharedHealthCheckEndpointGuard.DefaultSharedEndpointRoute, message, StringComparison.Ordinal);
        Assert.Contains("OrchardCore_HealthChecks:Url", message, StringComparison.Ordinal);
        Assert.Contains("AllowUnsafeSharedEndpointRoute", message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildHazardMessage_ReturnsNull_WhenTheOperatorAcknowledgedTheRoute()
    {
        Assert.Null(SharedHealthCheckEndpointGuard.BuildHazardMessage(configuredRoute: null, acknowledged: true));
    }

    [Fact]
    public void BuildHazardMessage_ReturnsNull_WhenTheRouteWasMovedOffALivenessName()
    {
        Assert.Null(SharedHealthCheckEndpointGuard.BuildHazardMessage("/health/aggregate", acknowledged: false));
    }

    [Fact]
    public void SharedEndpointHealthCheck_ReportsDegraded_WhenTheHazardWasRecorded()
    {
        var state = new SharedHealthEndpointHazardState();
        state.Record(SharedHealthCheckEndpointGuard.BuildHazardMessage(configuredRoute: null, acknowledged: false));

        var result = ContactCenterSharedEndpointHealthCheck.Evaluate(state);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("AllowUnsafeSharedEndpointRoute", result.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedEndpointHealthCheck_ReportsHealthy_WhenNoHazardWasRecorded()
    {
        var state = new SharedHealthEndpointHazardState();
        state.Record(SharedHealthCheckEndpointGuard.BuildHazardMessage("/health/aggregate", acknowledged: false));

        var result = ContactCenterSharedEndpointHealthCheck.Evaluate(state);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public void SharedEndpointHealthCheck_ReportsHealthy_BeforeTheRouteIsEvaluated()
    {
        var state = new SharedHealthEndpointHazardState();

        var result = ContactCenterSharedEndpointHealthCheck.Evaluate(state);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.False(state.HasBeenEvaluated);
    }
}
