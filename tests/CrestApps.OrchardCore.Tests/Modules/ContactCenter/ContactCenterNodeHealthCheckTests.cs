using CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Verifies the node-local readiness decision.
/// </summary>
/// <remarks>
/// This is the only check wired to readiness, so its verdict decides whether a node stays in the load
/// balancer. It must depend on nothing but this node's lifetime, and it must report unready during shutdown so
/// the node is drained before it stops accepting connections.
/// </remarks>
public sealed class ContactCenterNodeHealthCheckTests
{
    [Fact]
    public void Evaluate_ReportsHealthy_AfterStartupCompletes()
    {
        var result = ContactCenterNodeHealthCheck.Evaluate(CreateContext(), hasStarted: true, isStopping: false);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public void Evaluate_ReportsUnhealthy_BeforeStartupCompletes()
    {
        // A pod that has not finished starting must not receive traffic, otherwise a rolling deployment sends
        // calls to a node whose shells are still initializing.
        var result = ContactCenterNodeHealthCheck.Evaluate(CreateContext(), hasStarted: false, isStopping: false);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("not finished starting", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_ReportsUnhealthy_WhileShuttingDown()
    {
        // Reporting unready on shutdown is what lets the load balancer evict the node before the process stops
        // accepting connections. Without it, in-flight calls are dropped on every deployment.
        var result = ContactCenterNodeHealthCheck.Evaluate(CreateContext(), hasStarted: true, isStopping: true);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("drained", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_PrefersTheShutdownVerdict_WhenShutdownBeginsDuringStartup()
    {
        var result = ContactCenterNodeHealthCheck.Evaluate(CreateContext(), hasStarted: false, isStopping: true);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("drained", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_UsesTheConfiguredFailureStatus()
    {
        var context = CreateContext(HealthStatus.Degraded);

        var result = ContactCenterNodeHealthCheck.Evaluate(context, hasStarted: false, isStopping: false);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_ReportsUnhealthy_OnceTheHostSignalsStopping()
    {
        // Arrange
        var lifetime = new StubHostApplicationLifetime();
        var check = new ContactCenterNodeHealthCheck(lifetime);

        var beforeStopping = await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Act
        lifetime.SignalStopping();

        var afterStopping = await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HealthStatus.Healthy, beforeStopping.Status);
        Assert.Equal(HealthStatus.Unhealthy, afterStopping.Status);
    }

    private static HealthCheckContext CreateContext(HealthStatus failureStatus = HealthStatus.Unhealthy)
        => new()
        {
            Registration = new HealthCheckRegistration(
                "contactcenter-node",
                _ => throw new NotSupportedException("The registration factory is not used by these tests."),
                failureStatus,
                tags: null),
        };

    private sealed class StubHostApplicationLifetime : Microsoft.Extensions.Hosting.IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _started = new();
        private readonly CancellationTokenSource _stopping = new();
        private readonly CancellationTokenSource _stopped = new();

        public StubHostApplicationLifetime()
        {
            _started.Cancel();
        }

        public CancellationToken ApplicationStarted => _started.Token;

        public CancellationToken ApplicationStopping => _stopping.Token;

        public CancellationToken ApplicationStopped => _stopped.Token;

        public void SignalStopping() => _stopping.Cancel();

        public void StopApplication() => _stopping.Cancel();
    }
}
