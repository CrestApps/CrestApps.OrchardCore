namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Represents a provider request to create or update a live-call conference.
/// </summary>
public sealed class ContactCenterVoiceConferenceRequest
{
    /// <summary>
    /// Gets or sets the interaction identifier.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the provider call identifiers participating in the conference.
    /// </summary>
    public IList<string> ProviderCallIds { get; set; } = [];

    /// <summary>
    /// Gets or sets provider-specific metadata.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}
