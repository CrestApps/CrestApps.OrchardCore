using CrestApps.OrchardCore.Telephony.Models;

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
    /// Gets or sets the recording state the call was in before this request, as the platform last recorded it.
    /// </summary>
    /// <remarks>
    /// A request for <see cref="RecordingState.Recording"/> is a resume when the call was
    /// <see cref="RecordingState.Paused"/>, and a fresh start otherwise. A provider that cannot tell the two apart from
    /// the call itself reads this rather than probing.
    /// </remarks>
    public RecordingState PreviousState { get; set; }

    /// <summary>
    /// Gets or sets provider-specific metadata.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}
