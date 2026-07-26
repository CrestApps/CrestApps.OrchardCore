namespace CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;

/// <summary>
/// Validates the route of the shared, unfiltered health-check endpoint contributed by the
/// <c>OrchardCore.HealthChecks</c> module.
/// </summary>
/// <remarks>
/// That module maps a single endpoint with no registration predicate, so it aggregates every check registered
/// by every enabled module. Once Contact Center is enabled, that aggregate includes dependency checks such as
/// the event outbox backlog. An endpoint whose route claims liveness but whose content reports readiness is a
/// trap: wiring it to an orchestrator's liveness probe turns a slow outbox into a restart loop, and restarting
/// a node cannot drain an outbox. Contact Center is what makes that endpoint dangerous, so it refuses to
/// introduce the hazard silently.
/// </remarks>
public static class SharedHealthCheckEndpointGuard
{
    /// <summary>
    /// The route the <c>OrchardCore.HealthChecks</c> module uses when no route is configured.
    /// </summary>
    public const string DefaultSharedEndpointRoute = "/health/live";

    private static readonly string[] _livenessSegments = ["live", "liveness"];

    /// <summary>
    /// Determines whether the shared aggregate endpoint's route claims to be a liveness probe.
    /// </summary>
    /// <param name="configuredRoute">The configured route, or <see langword="null"/> when unset.</param>
    /// <returns><see langword="true"/> when the effective route claims liveness.</returns>
    public static bool IsUnsafeRoute(string configuredRoute)
    {
        var effectiveRoute = string.IsNullOrWhiteSpace(configuredRoute)
            ? DefaultSharedEndpointRoute
            : configuredRoute.Trim();

        var lastSegment = effectiveRoute
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();

        if (lastSegment is null)
        {
            return false;
        }

        return _livenessSegments.Contains(lastSegment, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Throws when the shared aggregate endpoint is named as a liveness probe and the operator has not
    /// acknowledged the risk.
    /// </summary>
    /// <param name="configuredRoute">The configured route, or <see langword="null"/> when unset.</param>
    /// <param name="acknowledged">Whether the operator accepted the shared endpoint's route.</param>
    /// <exception cref="InvalidOperationException">The route claims liveness and was not acknowledged.</exception>
    public static void Validate(string configuredRoute, bool acknowledged)
    {
        if (acknowledged || !IsUnsafeRoute(configuredRoute))
        {
            return;
        }

        var effectiveRoute = string.IsNullOrWhiteSpace(configuredRoute)
            ? DefaultSharedEndpointRoute
            : configuredRoute.Trim();

        throw new InvalidOperationException(
            $"The OrchardCore.HealthChecks module is enabled and maps its aggregate endpoint at '{effectiveRoute}'. " +
            "That endpoint applies no registration filter, so it reports the Contact Center dependency checks as " +
            "well. Using it as a liveness probe restarts healthy nodes whenever a dependency degrades, and a " +
            "restart cannot drain an event outbox. Set 'OrchardCore_HealthChecks:Url' to a route that does not " +
            "claim liveness, such as '/health/aggregate', and probe '/health/process' for liveness and " +
            "'api/contact-center/health/ready' for readiness. To keep the current route anyway, set " +
            "'CrestApps_ContactCenter:HealthChecks:AllowUnsafeSharedEndpointRoute' to true.");
    }
}
