using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;

/// <summary>
/// Reports whether the shared <c>OrchardCore.HealthChecks</c> aggregate endpoint is named as a liveness probe
/// while Contact Center is enabled.
/// </summary>
/// <remarks>
/// This is an alerting signal only; it is tagged as a dependency check and never as a readiness check. Failing
/// readiness here would drain the node — the exact restart behavior the hazard warns against — so the check
/// reports <see cref="HealthStatus.Degraded"/> at most and leaves rotation untouched. It exists so the hazard is
/// visible on the dependency-diagnostics probe and admin health surface after the tenant boots, instead of the
/// tenant refusing to boot at all.
/// </remarks>
public sealed class ContactCenterSharedEndpointHealthCheck : IHealthCheck
{
    private readonly SharedHealthEndpointHazardState _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterSharedEndpointHealthCheck"/> class.
    /// </summary>
    /// <param name="state">The recorded shared-endpoint hazard verdict for this tenant.</param>
    public ContactCenterSharedEndpointHealthCheck(SharedHealthEndpointHazardState state)
    {
        _state = state;
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Task.FromResult(Evaluate(_state));
    }

    /// <summary>
    /// Decides the verdict from the recorded hazard state.
    /// </summary>
    /// <param name="state">The recorded shared-endpoint hazard verdict for this tenant.</param>
    /// <returns>A degraded verdict when the hazard is present; otherwise a healthy verdict.</returns>
    public static HealthCheckResult Evaluate(SharedHealthEndpointHazardState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!state.HasBeenEvaluated)
        {
            return HealthCheckResult.Healthy("The shared health endpoint route has not been evaluated yet.");
        }

        var hazardMessage = state.HazardMessage;

        if (hazardMessage is null)
        {
            return HealthCheckResult.Healthy("The shared health endpoint route does not claim liveness, or the operator acknowledged it.");
        }

        return new HealthCheckResult(HealthStatus.Degraded, hazardMessage);
    }
}
