using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;

/// <summary>
/// Reports whether this deployment satisfies the topology its operator declared.
/// </summary>
/// <remarks>
/// This is the one readiness check that observes a condition every node shares, and the exception is
/// deliberate. Readiness normally excludes shared conditions because a shared <em>dependency</em> failure would
/// drain the whole fleet and turn a degraded database into a total outage. A topology violation is a different
/// kind of condition: it cannot self-heal, no amount of waiting fixes it, and continuing to serve traffic is
/// itself the failure being prevented. Refusing traffic on an uncertified deployment is the correct outcome,
/// not collateral damage.
/// </remarks>
public sealed class ContactCenterTopologyHealthCheck : IHealthCheck
{
    private readonly ContactCenterTopologyState _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterTopologyHealthCheck"/> class.
    /// </summary>
    /// <param name="state">The recorded topology verdict for this tenant.</param>
    public ContactCenterTopologyHealthCheck(ContactCenterTopologyState state)
    {
        _state = state;
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Task.FromResult(Evaluate(context, _state.Result));
    }

    /// <summary>
    /// Decides the readiness verdict from the recorded topology result.
    /// </summary>
    /// <param name="context">The health check context supplying the configured failure status.</param>
    /// <param name="result">The recorded verdict, or <see langword="null"/> when validation has not run yet.</param>
    /// <returns>The readiness verdict for this deployment.</returns>
    public static HealthCheckResult Evaluate(
        HealthCheckContext context,
        ContactCenterTopologyValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (result is null)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "The Contact Center deployment topology has not been validated yet.");
        }

        if (!result.IsSatisfied)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "This deployment does not satisfy the Contact Center topology it declared: " + string.Join(" ", result.Failures));
        }

        if (result.DeclaredProfileId is null)
        {
            return HealthCheckResult.Healthy("No Contact Center topology profile is declared; this deployment does not claim production support.");
        }

        var profile = ContactCenterTopologyProfiles.Find(result.DeclaredProfileId);

        if (profile is { IsProduction: true, MaximumApplicationNodes: 1 })
        {
            // A single-active-node production profile carries a constraint this check cannot enforce: it verifies
            // the declared infrastructure prerequisites but never counts how many application nodes are actually
            // running, so two hosts can each declare this profile and both report healthy while double-claiming
            // the same real-time voice application on different nodes. Surfacing the operator responsibility in
            // the healthy verdict itself keeps it in front of whoever inspects health, not only in the docs.
            return HealthCheckResult.Healthy(
                $"This deployment satisfies the '{result.DeclaredProfileId}' Contact Center topology. " +
                "This profile certifies exactly one active application node. This check verifies the declared " +
                "infrastructure prerequisites but cannot detect a second active node claiming the same real-time " +
                "voice application, so running a single active node is an operator responsibility. See " +
                "docs/telephony/index.md.");
        }

        return HealthCheckResult.Healthy($"This deployment satisfies the '{result.DeclaredProfileId}' Contact Center topology.");
    }
}
