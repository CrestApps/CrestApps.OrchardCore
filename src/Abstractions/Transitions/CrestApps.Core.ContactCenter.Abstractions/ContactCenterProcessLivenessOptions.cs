namespace CrestApps.Core.ContactCenter;

/// <summary>
/// Configures the host-level process liveness probe.
/// </summary>
public sealed class ContactCenterProcessLivenessOptions
{
    /// <summary>
    /// Gets or sets the path the process liveness probe answers on.
    /// </summary>
    /// <remarks>
    /// The default deliberately avoids <c>/health/live</c>, the route a host's own aggregate health endpoint
    /// conventionally takes, because host middleware short-circuits before routing and would otherwise replace
    /// that endpoint with an unconditional success for every tenant in the process.
    /// </remarks>
    public string Path { get; set; } = ContactCenterConstants.HealthChecks.ProcessLivenessPath;

    /// <summary>
    /// Gets or sets the delegate that resolves the route of the host's own shared health endpoint, so the
    /// liveness path can be checked against it.
    /// </summary>
    /// <remarks>
    /// Which configuration key names that route, and what it falls back to when the key is unset, is the
    /// host's knowledge rather than this package's. A host with no shared health endpoint leaves this
    /// <see langword="null"/> and nothing is checked; a host that has one resolves the effective route,
    /// default included, so the check sees the route that will actually be served.
    /// </remarks>
    public Func<IServiceProvider, string> SharedHealthEndpointRouteResolver { get; set; }
}
