using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// A recorded voice call, listed on the call recordings page and played back from the encrypted media store.
/// </summary>
/// <remarks>
/// A recording is cataloged on its own rather than read off the call it belongs to, because the calls that can be
/// recorded are not all of one kind: a routed or dialed call is a Contact Center interaction, but an automated voice
/// agent's call often never becomes one, and a number an agent dials on the soft phone's keypad is only a telephony
/// call. Each source fills in what it knows, and the page searches one table.
/// </remarks>
public sealed class CallRecording : CatalogItem
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
    /// Gets or sets the provider's identifier of the recording. It is unique per recording, so a redelivered
    /// "recording saved" notification finds the entry it already made.
    /// </summary>
    public string ProviderRecordingId { get; set; }

    /// <summary>
    /// Gets or sets the reference the recording is stored under in the media store.
    /// </summary>
    public string StorageReference { get; set; }

    /// <summary>
    /// Gets or sets the audio format, such as <c>mp3</c>.
    /// </summary>
    public string Format { get; set; }

    /// <summary>
    /// Gets or sets the Contact Center interaction that was recorded, when the call is one.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the CRM activity the call belongs to, when it has one. An automated voice agent's transcript is
    /// found through it.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the automated conversation session behind the call, when an AI voice agent talked on it. Its
    /// transcript is shown beside the recording. When empty, it is looked up through <see cref="ActivityItemId"/>.
    /// </summary>
    public string AiSessionId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the soft phone call the recording belongs to, for a number dialed on the keypad.
    /// </summary>
    public string TelephonyCallId { get; set; }

    /// <summary>
    /// Gets or sets the user identifier of the agent on the call. Empty for an automated voice agent's call that was
    /// never handed to a person.
    /// </summary>
    public string AgentUserId { get; set; }

    /// <summary>
    /// Gets or sets the customer's phone number or address.
    /// </summary>
    public string CustomerAddress { get; set; }

    /// <summary>
    /// Gets or sets who placed the call.
    /// </summary>
    public InteractionDirection Direction { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the recording started. Transcript lines are placed on the recording's timeline
    /// relative to it.
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the recording ended, when known.
    /// </summary>
    public DateTime? EndedUtc { get; set; }

    /// <summary>
    /// Gets or sets the length of the recording in seconds, when known.
    /// </summary>
    public double DurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the recording landed in the media store. A recording is only playable once it has.
    /// </summary>
    public DateTime? StoredUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the recording was erased. An erased recording is no longer listed or played.
    /// </summary>
    public DateTime? ErasedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the entry was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
