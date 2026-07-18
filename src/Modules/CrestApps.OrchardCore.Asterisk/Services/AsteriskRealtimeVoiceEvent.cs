using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed class AsteriskRealtimeVoiceEvent
{
    /// <summary>
    /// Gets or sets the configured telephony provider name that emitted the event.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider call identifier used by the shared telephony abstractions.
    /// </summary>
    public string CallId { get; set; }

    /// <summary>
    /// Gets or sets the raw Asterisk event type.
    /// </summary>
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the source address used by the shared telephony abstractions.
    /// </summary>
    public string FromAddress { get; set; }

    /// <summary>
    /// Gets or sets the destination address used by the shared telephony abstractions.
    /// </summary>
    public string ToAddress { get; set; }

    /// <summary>
    /// Gets or sets the Asterisk channel identifier associated with the event.
    /// </summary>
    public string ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the parent Asterisk channel identifier when the event payload exposes one.
    /// </summary>
    public string ParentChannelId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the event represents a fresh inbound external call.
    /// </summary>
    public bool IsInbound { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the event belongs to a channel originated by this module.
    /// </summary>
    public bool IsOwnedOrigination { get; set; }

    /// <summary>
    /// Gets or sets the dialed number from the inbound DID or connected party information.
    /// </summary>
    public string DialedNumber { get; set; }

    /// <summary>
    /// Gets or sets the caller number from the Asterisk channel payload.
    /// </summary>
    public string CallerNumber { get; set; }

    /// <summary>
    /// Gets or sets the interaction correlation identifier carried by the Asterisk channel variables.
    /// </summary>
    public string InteractionCorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the normalized call state.
    /// </summary>
    public CallState State { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the channel mute state is known and enabled.
    /// </summary>
    public bool? IsMuted { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the channel is on hold.
    /// </summary>
    public bool IsOnHold { get; set; }

    /// <summary>
    /// Gets or sets the UTC time reported by the Asterisk event.
    /// </summary>
    public DateTime? OccurredUtc { get; set; }

    /// <summary>
    /// Gets or sets the deterministic idempotency key for the raw event payload.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the event appears to represent a conference bridge.
    /// </summary>
    public bool? IsConference { get; set; }

    /// <summary>
    /// Gets or sets the participant count reported by the bridge payload, when present.
    /// </summary>
    public int? ParticipantCount { get; set; }

    /// <summary>
    /// Gets or sets provider-specific metadata captured from the raw event.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
