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
    /// Gets or sets provider-specific metadata.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}
