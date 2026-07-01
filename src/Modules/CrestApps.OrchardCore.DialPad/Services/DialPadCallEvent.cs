using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.DialPad.Services;

/// <summary>
/// Represents the normalized fields of a DialPad call-event webhook payload.
/// </summary>
public sealed class DialPadCallEvent
{
    /// <summary>
    /// Gets or sets the DialPad call identifier.
    /// </summary>
    [JsonPropertyName("call_id")]
    public string CallId { get; set; }

    /// <summary>
    /// Gets or sets the DialPad call state (for example ringing, connected, hangup).
    /// </summary>
    [JsonPropertyName("state")]
    public string State { get; set; }

    /// <summary>
    /// Gets or sets the call direction (inbound or outbound).
    /// </summary>
    [JsonPropertyName("direction")]
    public string Direction { get; set; }

    /// <summary>
    /// Gets or sets the external party number (the customer number).
    /// </summary>
    [JsonPropertyName("external_number")]
    public string ExternalNumber { get; set; }

    /// <summary>
    /// Gets or sets the internal number the call was placed to (the dialed DID for inbound calls).
    /// </summary>
    [JsonPropertyName("internal_number")]
    public string InternalNumber { get; set; }

    /// <summary>
    /// Gets or sets the target number, used as a fallback for the dialed DID.
    /// </summary>
    [JsonPropertyName("target")]
    public string Target { get; set; }

    /// <summary>
    /// Gets or sets the contact display name supplied by DialPad, when available.
    /// </summary>
    [JsonPropertyName("contact_name")]
    public string ContactName { get; set; }

    /// <summary>
    /// Gets or sets the epoch milliseconds the event occurred.
    /// </summary>
    [JsonPropertyName("event_timestamp")]
    public long? EventTimestamp { get; set; }
}
