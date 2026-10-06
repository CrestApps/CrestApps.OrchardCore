using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// What a voice provider knows about a recording it just saved, handed to <c>ICallRecordingCatalog</c> to list it.
/// Whatever is left empty is filled from the recorded interaction, when there is one.
/// </summary>
public sealed class CallRecordingRegistration
{
    /// <summary>
    /// Gets or sets the kind of call that was recorded.
    /// </summary>
    public CallRecordingSource Source { get; set; }

    /// <summary>
    /// Gets or sets the voice provider that captured the recording.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider's identifier of the recording.
    /// </summary>
    public string ProviderRecordingId { get; set; }

    /// <summary>
    /// Gets or sets the reference the recording will be stored under in the media store.
    /// </summary>
    public string StorageReference { get; set; }

    /// <summary>
    /// Gets or sets the audio format.
    /// </summary>
    public string Format { get; set; }

    /// <summary>
    /// Gets or sets the recorded Contact Center interaction, when the call is one.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the CRM activity the call belongs to.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the automated conversation session behind the call, when known.
    /// </summary>
    public string AiSessionId { get; set; }

    /// <summary>
    /// Gets or sets the soft phone call the recording belongs to.
    /// </summary>
    public string TelephonyCallId { get; set; }

    /// <summary>
    /// Gets or sets the user identifier of the agent on the call.
    /// </summary>
    public string AgentUserId { get; set; }

    /// <summary>
    /// Gets or sets the customer's phone number or address.
    /// </summary>
    public string CustomerAddress { get; set; }

    /// <summary>
    /// Gets or sets who placed the call, when the provider knows.
    /// </summary>
    public InteractionDirection? Direction { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the recording started, when the provider reports it.
    /// </summary>
    public DateTime? StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the recording ended, when the provider reports it.
    /// </summary>
    public DateTime? EndedUtc { get; set; }
}
