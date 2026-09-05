namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Represents the normalized fields of a Telnyx call-event webhook payload, flattened from the Telnyx
/// <c>data.payload</c> envelope so the rest of the pipeline does not depend on the wire shape.
/// </summary>
public sealed class TelnyxCallEvent
{
    /// <summary>
    /// Gets or sets the Telnyx event type (for example <c>call.initiated</c>, <c>call.answered</c>,
    /// <c>call.hangup</c>).
    /// </summary>
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the Telnyx call control identifier that call-control actions target.
    /// </summary>
    public string CallControlId { get; set; }

    /// <summary>
    /// Gets or sets the Telnyx call leg identifier.
    /// </summary>
    public string CallLegId { get; set; }

    /// <summary>
    /// Gets or sets the Telnyx call session identifier that groups the legs of a call.
    /// </summary>
    public string CallSessionId { get; set; }

    /// <summary>
    /// Gets or sets the Telnyx connection (Call Control application) identifier.
    /// </summary>
    public string ConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the call direction reported by Telnyx (<c>incoming</c> or <c>outgoing</c>).
    /// </summary>
    public string Direction { get; set; }

    /// <summary>
    /// Gets or sets the calling party address.
    /// </summary>
    public string From { get; set; }

    /// <summary>
    /// Gets or sets the called party address (the dialed DID for inbound calls).
    /// </summary>
    public string To { get; set; }

    /// <summary>
    /// Gets or sets the Telnyx call state, when the payload includes one.
    /// </summary>
    public string State { get; set; }

    /// <summary>
    /// Gets or sets the Telnyx hangup cause, when the event is a hangup.
    /// </summary>
    public string HangupCause { get; set; }

    /// <summary>
    /// Gets or sets who ended the call (for example <c>caller</c>, <c>callee</c>, or <c>telnyx</c>), when the
    /// event is a hangup. Useful for distinguishing a rejection by the far end from one issued by Telnyx.
    /// </summary>
    public string HangupSource { get; set; }

    /// <summary>
    /// Gets or sets the SIP response code Telnyx reported for the hangup, when present.
    /// </summary>
    public string SipHangupCause { get; set; }

    /// <summary>
    /// Gets or sets the recording identifier, when the event carries one.
    /// </summary>
    public string RecordingId { get; set; }

    /// <summary>
    /// Gets or sets the recognized transcript text, when the event is a <c>call.transcription</c>.
    /// </summary>
    public string TranscriptionText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the transcript is final (an utterance the caller finished)
    /// rather than an interim hypothesis, when the event is a <c>call.transcription</c>.
    /// </summary>
    public bool TranscriptionIsFinal { get; set; }

    /// <summary>
    /// Gets or sets the Telnyx event identifier used for delivery de-duplication.
    /// </summary>
    public string EventId { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the event occurred.
    /// </summary>
    public DateTime? OccurredUtc { get; set; }

    /// <summary>
    /// Gets or sets the decoded Telnyx <c>client_state</c> the platform attached when it originated the leg.
    /// Telnyx echoes this base64 value back on every event for the leg; the outbound-bridge orchestration
    /// uses it to correlate the agent and destination legs it created.
    /// </summary>
    public string ClientState { get; set; }
}
