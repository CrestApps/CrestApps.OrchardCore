namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Represents a provider request to change recording state for a live call.
/// </summary>
public sealed class ContactCenterVoiceRecordingRequest
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
    /// Gets or sets the requested recording state.
    /// </summary>
    public RecordingState State { get; set; }

    /// <summary>
    /// Gets or sets provider-specific metadata.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}
