namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Configures the host-level process liveness probe.
/// </summary>
public sealed class ContactCenterProcessLivenessOptions
{
    /// <summary>
    /// Gets or sets the path the process liveness probe answers on.
    /// </summary>
    /// <remarks>
    /// The default deliberately avoids <c>/health/live</c>, the default route of the
    /// <c>OrchardCore.HealthChecks</c> module, because host middleware short-circuits before routing and would
    /// otherwise replace that module's endpoint with an unconditional success for every tenant in the process.
    /// </remarks>
    public string Path { get; set; } = ContactCenterConstants.HealthChecks.ProcessLivenessPath;
}
