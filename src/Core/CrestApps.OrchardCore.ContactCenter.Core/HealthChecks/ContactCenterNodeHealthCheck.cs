using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;

/// <summary>
/// Reports whether this node should receive traffic, based only on node-local state.
/// </summary>
/// <remarks>
/// This is the only check wired to the readiness probe, and it consults no external dependency on purpose.
/// <para>
/// Readiness removes a node from the load balancer. A condition shared by every node — a database outage, a
/// growing outbox backlog, an unreachable provider — is reported identically by every node, so wiring it to
/// readiness drains the entire fleet at once and converts a degraded dependency into a total outage. Readiness
/// must therefore only reflect conditions that actually differ between nodes: whether this process finished
/// starting, and whether it is shutting down.
/// </para>
/// <para>
/// Dependency health is still observed — it is reported through the dependency probe and the metrics — but it
/// is an alerting signal, never a routing signal.
/// </para>
/// </remarks>
public sealed class ContactCenterNodeHealthCheck : IHealthCheck
{
    private readonly IHostApplicationLifetime _lifetime;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterNodeHealthCheck"/> class.
    /// </summary>
    /// <param name="lifetime">The host lifetime used to observe startup and shutdown of this node.</param>
    public ContactCenterNodeHealthCheck(IHostApplicationLifetime lifetime)
    {
        _lifetime = lifetime;
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Task.FromResult(Evaluate(
            context,
            hasStarted: _lifetime.ApplicationStarted.IsCancellationRequested,
            isStopping: _lifetime.ApplicationStopping.IsCancellationRequested));
    }

    /// <summary>
    /// Decides the node readiness verdict from the observed host lifetime state.
    /// </summary>
    /// <param name="context">The health check context supplying the configured failure status.</param>
    /// <param name="hasStarted">Whether the host has signalled that startup completed.</param>
    /// <param name="isStopping">Whether the host has signalled that shutdown has begun.</param>
    /// <returns>The readiness verdict for this node.</returns>
    /// <remarks>
    /// Shutdown is evaluated before startup so a node that begins draining during startup still reports
    /// draining. Reporting unready while stopping is what lets a load balancer evict the node before the
    /// process stops accepting connections, which is the difference between a graceful deployment and dropped
    /// calls.
    /// </remarks>
    public static HealthCheckResult Evaluate(HealthCheckContext context, bool hasStarted, bool isStopping)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (isStopping)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "This node is shutting down and should be drained.");
        }

        if (!hasStarted)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "This node has not finished starting.");
        }

        return HealthCheckResult.Healthy("This node has started and is accepting traffic.");
    }
}
