using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;

/// <summary>
/// Surfaces the tenant's live active-call gauge — the number of call sessions that have not yet ended — as
/// health-check data. This is the count an operator weighs before draining a node during a handover: how many
/// live calls a stop would interrupt.
/// </summary>
/// <remarks>
/// The count is surfaced as health data rather than as an <see cref="System.Diagnostics.Metrics"/> observable
/// gauge on purpose. An observable-gauge callback is synchronous and runs with no ambient tenant scope, so it
/// cannot safely issue the per-tenant store query this count requires, and a process-global count would be wrong
/// across tenants and nodes. A health check already runs inside the tenant scope with an async body, so it is the
/// correct seam for a store-backed gauge. The check reports the count and stays healthy whenever it can read it;
/// it deliberately does not degrade on a high count because the acceptable ceiling is deployment specific.
/// </remarks>
public sealed class ContactCenterActiveCallsHealthCheck : IHealthCheck
{
    private readonly ICallSessionStore _callSessionStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterActiveCallsHealthCheck"/> class.
    /// </summary>
    /// <param name="callSessionStore">The call session store used to count active calls.</param>
    public ContactCenterActiveCallsHealthCheck(ICallSessionStore callSessionStore)
    {
        _callSessionStore = callSessionStore;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var activeCalls = await _callSessionStore.CountActiveAsync(cancellationToken);

            var data = new Dictionary<string, object>
            {
                ["active_calls"] = activeCalls,
            };

            var description = string.Format(
                CultureInfo.InvariantCulture,
                "{0} active call(s).",
                activeCalls);

            return HealthCheckResult.Healthy(description, data);
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Unable to read the Contact Center active-call count.", ex);
        }
    }
}
