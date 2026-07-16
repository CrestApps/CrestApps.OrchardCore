namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Describes a request to attach a bidirectional media session to an existing provider call.
/// </summary>
public sealed class ContactCenterVoiceMediaSessionRequest
{
    /// <summary>
    /// Gets or sets the provider call identifier.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the Contact Center interaction identifier.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the preferred format for caller audio received from the provider.
    /// </summary>
    public ContactCenterVoiceMediaFormat PreferredIncomingFormat { get; set; }

    /// <summary>
    /// Gets or sets the preferred format for application audio written to the provider.
    /// </summary>
    public ContactCenterVoiceMediaFormat PreferredOutgoingFormat { get; set; }

    /// <summary>
    /// Gets or sets provider-specific metadata required to open the media session.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
