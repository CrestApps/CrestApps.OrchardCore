using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;

/// <summary>
/// Surfaces the tenant's queued-interaction backlog gauge — the number of interactions waiting for an agent
/// across every queue — as health-check data. This is the routed work an operator weighs before draining a node
/// during a handover: how much waiting work the deployment is still carrying.
/// </summary>
/// <remarks>
/// The count is surfaced as health data rather than as an <see cref="System.Diagnostics.Metrics"/> observable
/// gauge on purpose. An observable-gauge callback is synchronous and runs with no ambient tenant scope, so it
/// cannot safely issue the per-tenant store query this count requires, and a process-global count would be wrong
/// across tenants and nodes. A health check already runs inside the tenant scope with an async body, so it is the
/// correct seam for a store-backed gauge. The check reports the count and stays healthy whenever it can read it;
/// it deliberately does not degrade on a high count because the acceptable ceiling is deployment specific.
/// </remarks>
public sealed class ContactCenterQueueBacklogHealthCheck : IHealthCheck
{
    private readonly IQueueItemStore _queueItemStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterQueueBacklogHealthCheck"/> class.
    /// </summary>
    /// <param name="queueItemStore">The queue item store used to count waiting interactions.</param>
    public ContactCenterQueueBacklogHealthCheck(IQueueItemStore queueItemStore)
    {
        _queueItemStore = queueItemStore;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queuedInteractions = await _queueItemStore.CountAllWaitingAsync(cancellationToken);

            var data = new Dictionary<string, object>
            {
                ["queued_interactions"] = queuedInteractions,
            };

            var description = string.Format(
                CultureInfo.InvariantCulture,
                "{0} queued interaction(s).",
                queuedInteractions);

            return HealthCheckResult.Healthy(description, data);
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Unable to read the Contact Center queued-interaction count.", ex);
        }
    }
}
