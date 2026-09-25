namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Represents a provider request for a supervisor monitoring engagement.
/// </summary>
public sealed class ContactCenterVoiceMonitoringRequest
{
    /// <summary>
    /// Gets or sets the interaction identifier.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the provider call identifier.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the supervisor identifier.
    /// </summary>
    public string SupervisorId { get; set; }

    /// <summary>
    /// Gets or sets the monitoring mode.
    /// </summary>
    public MonitorMode Mode { get; set; }

    /// <summary>
    /// Gets or sets the provider identifier of the agent's leg on the call, when the platform knows it: the leg a
    /// whispering supervisor is heard by, and the one a takeover releases.
    /// </summary>
    public string AgentLegId { get; set; }

    /// <summary>
    /// Gets or sets the provider identifier of the supervisor's own leg of an engagement already started, for a
    /// stop, a mode change or a takeover.
    /// </summary>
    public string SupervisorLegId { get; set; }

    /// <summary>
    /// Gets or sets the one-off token the supervisor's phone was told to expect, which a provider that rings the
    /// supervisor's own soft phone attaches to that leg so the phone answers it, and nothing else, by itself.
    /// </summary>
    public string MonitorToken { get; set; }

    /// <summary>
    /// Gets or sets provider-specific metadata.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}
