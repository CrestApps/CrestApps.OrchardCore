namespace CrestApps.OrchardCore.Dialpad.Services;

/// <summary>
/// Represents the normalized fields of a Dialpad call-event webhook payload.
/// </summary>
public sealed class DialpadCallEvent
{
    /// <summary>
    /// Gets or sets the Dialpad call identifier.
    /// </summary>
    public string CallId { get; set; }

    /// <summary>
    /// Gets or sets the Dialpad call state (for example ringing, connected, hangup).
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
    /// Gets or sets the selected outbound caller id Dialpad reported for the call.
    /// </summary>
    public string SelectedCallerId { get; set; }

    /// <summary>
    /// Gets or sets the target number or endpoint, used as a fallback for the dialed DID.
    /// </summary>
    public string Target { get; set; }

    /// <summary>
    /// Gets or sets the target Dialpad entity identifier, when the payload includes one.
    /// </summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Gets or sets the target Dialpad entity type, such as user.
    /// </summary>
    public string TargetType { get; set; }

    /// <summary>
    /// Gets or sets the target email address.
    /// </summary>
    public string TargetEmail { get; set; }

    /// <summary>
    /// Gets or sets the target phone number.
    /// </summary>
    public string TargetPhone { get; set; }

    /// <summary>
    /// Gets or sets the target display name.
    /// </summary>
    public string TargetName { get; set; }

    /// <summary>
    /// Gets or sets the contact display name supplied by Dialpad, when available.
    /// </summary>
    public string ContactName { get; set; }

    /// <summary>
    /// Gets or sets the epoch milliseconds the event occurred.
    /// </summary>
    public long? EventTimestamp { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Dialpad reports the call as muted.
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
