namespace CrestApps.OrchardCore.DialPad.Services;

/// <summary>
/// Represents the normalized fields of a DialPad call-event webhook payload.
/// </summary>
public sealed class DialPadCallEvent
{
    /// <summary>
    /// Gets or sets the DialPad call identifier.
    /// </summary>
    public string CallId { get; set; }

    /// <summary>
    /// Gets or sets the DialPad call state (for example ringing, connected, hangup).
    /// </summary>
    public string State { get; set; }

    /// <summary>
    /// Gets or sets the call direction (inbound or outbound).
    /// </summary>
    public string Direction { get; set; }

    /// <summary>
    /// Gets or sets the external party number (the customer number).
    /// </summary>
    public string ExternalNumber { get; set; }

    /// <summary>
    /// Gets or sets the internal number the call was placed to (the dialed DID for inbound calls).
    /// </summary>
    public string InternalNumber { get; set; }

    /// <summary>
    /// Gets or sets the target number, used as a fallback for the dialed DID.
    /// </summary>
    public string Target { get; set; }

    /// <summary>
    /// Gets or sets the contact display name supplied by DialPad, when available.
    /// </summary>
    public string ContactName { get; set; }

    /// <summary>
    /// Gets or sets the epoch milliseconds the event occurred.
    /// </summary>
    public long? EventTimestamp { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether DialPad reports the call as muted.
    /// </summary>
    public bool? IsMuted { get; set; }

    /// <summary>
    /// Gets or sets the provider-reported recording state, when present.
    /// </summary>
    public string RecordingState { get; set; }

    /// <summary>
    /// Gets or sets the recording identifier, when the provider includes one.
    /// </summary>
    public string RecordingId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider reports the call as a conference or
    /// multi-party session.
    /// </summary>
    public bool? IsConference { get; set; }

    /// <summary>
    /// Gets or sets the number of active participants reported by the provider.
    /// </summary>
    public int? ParticipantCount { get; set; }
}
