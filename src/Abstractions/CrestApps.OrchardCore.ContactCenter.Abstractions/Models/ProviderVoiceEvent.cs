namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Represents a provider-neutral voice event after a telephony provider or PBX webhook has been
/// normalized. It is the single entry point through which provider call-state changes (ringing,
/// answered, held, transferred, ended, failed) flow into the Contact Center so the call session,
/// interaction, and analytics projections stay in sync regardless of the provider.
/// </summary>
public sealed class ProviderVoiceEvent
{
    /// <summary>
    /// Gets or sets the technical name of the provider that produced the event.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider-specific identifier of the call the event relates to.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the provider-specific call leg identifier, when the channel has leg-level tracking.
    /// </summary>
    public string ProviderLegId { get; set; }

    /// <summary>
    /// Gets or sets the normalized call state the event represents.
    /// </summary>
    public ContactCenterCallState State { get; set; }

    /// <summary>
    /// Gets or sets the address of the calling party, when supplied.
    /// </summary>
    public string FromAddress { get; set; }

    /// <summary>
    /// Gets or sets the address of the called party, when supplied.
    /// </summary>
    public string ToAddress { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the event occurred. When not supplied, the current time is used.
    /// </summary>
    public DateTime? OccurredUtc { get; set; }

    /// <summary>
    /// Gets or sets an idempotency key that uniquely identifies this provider event so duplicate
    /// deliveries can be de-duplicated. When set, replays of the same event are ignored.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets an optional provider-supplied monotonic sequence number for the call stream. When
    /// supplied, ingestion uses it as the authoritative ordering high-water mark and rejects stale or
    /// equal-order deliveries. Providers that only supply timestamps or idempotency keys leave it
    /// <see langword="null"/> and ingestion falls back to timestamp-based ordering.
    /// </summary>
    public long? SequenceNumber { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider reports the call as muted.
    /// When <see langword="null"/>, the event does not change the current mute state.
    /// </summary>
    public bool? IsMuted { get; set; }

    /// <summary>
    /// Gets or sets the provider-reported recording state.
    /// When <see langword="null"/>, the event does not change the current recording state.
    /// </summary>
    public RecordingState? RecordingState { get; set; }

    /// <summary>
    /// Gets or sets the provider recording reference for the session, when recording is active or retained.
    /// </summary>
    public string RecordingReference { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider reports the call as a conference or
    /// multi-party session. When <see langword="null"/>, the event does not change the current conference flag.
    /// </summary>
    public bool? IsConference { get; set; }

    /// <summary>
    /// Gets or sets the number of active participants the provider reports for the session.
    /// When <see langword="null"/>, the event does not change the current participant count.
    /// </summary>
    public int? ParticipantCount { get; set; }

    /// <summary>
    /// Gets or sets additional provider metadata to retain for troubleshooting.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}
