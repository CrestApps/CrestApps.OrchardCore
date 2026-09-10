namespace CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;

/// <summary>
/// Holds, for the life of the tenant shell, whether the shared <c>OrchardCore.HealthChecks</c> aggregate
/// endpoint is named as a liveness probe while Contact Center is enabled.
/// </summary>
/// <remarks>
/// Registered as a singleton per tenant shell. The verdict is established during activation from configuration
/// that can only change by rebuilding the shell, so it is immutable for the life of the shell. The value is the
/// actionable hazard message when the route is unsafe and unacknowledged; otherwise it is <see langword="null"/>.
/// <para>
/// The hazard is recorded rather than thrown: refusing the route by throwing during shell construction would
/// brick the tenant with no diagnostic surface, and the shared endpoint's shipped default already claims
/// liveness. Recording it lets a health check surface the hazard while the tenant stays reachable.
/// </para>
/// </remarks>
public sealed class SharedHealthEndpointHazardState
{
    private string _hazardMessage;
    private int _recorded;

    /// <summary>
    /// Gets the recorded hazard message, or <see langword="null"/> when the route is safe, acknowledged, or has
    /// not been evaluated yet.
    /// </summary>
    public string HazardMessage => Volatile.Read(ref _hazardMessage);

    /// <summary>
    /// Gets a value indicating whether the hazard verdict has been recorded for this tenant shell.
    /// </summary>
    public bool HasBeenEvaluated => Volatile.Read(ref _recorded) != 0;

    /// <summary>
    /// Gets a value indicating whether the shared aggregate endpoint currently presents the liveness hazard.
    /// </summary>
    public bool IsHazardous => Volatile.Read(ref _hazardMessage) is not null;

    /// <summary>
    /// Records the hazard verdict for this tenant shell.
    /// </summary>
    /// <param name="hazardMessage">The hazard message, or <see langword="null"/> when the route is safe or acknowledged.</param>
    public void Record(string hazardMessage)
    {
        Volatile.Write(ref _hazardMessage, hazardMessage);
        Volatile.Write(ref _recorded, 1);
    }
}
