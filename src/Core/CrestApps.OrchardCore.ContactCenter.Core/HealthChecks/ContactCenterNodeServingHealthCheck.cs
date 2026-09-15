using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;

/// <summary>
/// Reports whether this node can still reach the tenant's durable store well enough to serve traffic.
/// </summary>
/// <remarks>
/// This check exists because "shared dependency" does not mean "fails identically on every node". A pod with an
/// exhausted connection pool, a stale DNS entry, an expired trust store, or exhausted outbound ports will fail
/// every database call while its peers are perfectly healthy, and nothing else in the readiness contract would
/// notice. Such a node keeps accepting its share of calls and failing all of them.
/// <para>
/// It is nonetheless <b>disabled by default</b>. When the store itself is down every node observes the failure,
/// so the gate would drain the whole fleet — the very failure mode the readiness split exists to prevent.
/// Enabling it is therefore safe only where the load balancer fails open once too few targets remain healthy
/// (for example an Envoy or Istio panic threshold), or where partial drain is preferable to partial failure.
/// </para>
/// <para>
/// When disabled the check performs no I/O at all and reports healthy immediately, so readiness stays free.
/// </para>
/// </remarks>
public sealed class ContactCenterNodeServingHealthCheck : IHealthCheck
{
    private readonly IContactCenterOutboxStore _outboxStore;
    private readonly NodeServingStateTracker _tracker;
    private readonly IOptions<ContactCenterHealthCheckOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterNodeServingHealthCheck"/> class.
    /// </summary>
    /// <param name="outboxStore">The durable store used as a lightweight serving probe.</param>
    /// <param name="tracker">The per-node tracker that applies hysteresis to the observed outcomes.</param>
    /// <param name="options">The configured health check options.</param>
    public ContactCenterNodeServingHealthCheck(
        IContactCenterOutboxStore outboxStore,
        NodeServingStateTracker tracker,
        IOptions<ContactCenterHealthCheckOptions> options)
    {
        _outboxStore = outboxStore;
        _tracker = tracker;
        _options = options;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_options.Value.EnableNodeServingGate)
        {
            return HealthCheckResult.Healthy("The node serving gate is disabled.");
        }

        bool succeeded;

        try
        {
            await _outboxStore.CountByStatusAsync(OutboxMessageStatus.Completed, cancellationToken);
            succeeded = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A cancelled probe says nothing about this node, so it must not count as a failure.
            throw;
        }
        catch
        {
            succeeded = false;
        }

        return _tracker.Record(succeeded)
            ? HealthCheckResult.Healthy("This node can reach the Contact Center store.")
            : new HealthCheckResult(
                context.Registration.FailureStatus,
                "This node has failed consecutive store probes and should be drained.");
    }
}
