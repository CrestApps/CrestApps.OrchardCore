using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents a provider-neutral voice event after a telephony provider or PBX webhook has been
/// normalized. It is the single entry point through which provider call-state changes (ringing,
/// answered, held, transferred, ended, failed) flow into every consumer projection, so each projection
/// built on the same provider stream stays in sync regardless of the provider.
/// </summary>
/// <remarks>
/// The type is immutable. It is a public provider contract that ingestion also has to adjust — the provider
/// identity is canonicalized and the idempotency key is scoped by it — and while it was mutable those
/// adjustments were applied to the caller's own instance, so ingestion had to defend itself with a hand-written
/// copy whose completeness was a separate thing to get wrong. It was got wrong once: the copy dropped
/// <see cref="HangupCause"/>, and because a session infers a cause when none is supplied, every call reported
/// the inferred cause instead of the one the provider gave, with nothing anywhere to say the real one was lost.
/// Adjustments are now made with <see langword="with" />, which copies every member by construction.
/// </remarks>
public sealed record ProviderVoiceEvent
{
    private static readonly MetadataSnapshot _emptyMetadata =
        new(new Dictionary<string, string>(StringComparer.Ordinal));

    private readonly IReadOnlyDictionary<string, string> _metadata = _emptyMetadata;

    /// <summary>
    /// Gets the technical name of the provider that produced the event.
    /// </summary>
    public string ProviderName { get; init; }

    /// <summary>
    /// Gets the provider-specific identifier of the call the event relates to.
    /// </summary>
    public string ProviderCallId { get; init; }

    /// <summary>
    /// Gets the provider-specific call leg identifier, when the channel has leg-level tracking.
    /// </summary>
    public string ProviderLegId { get; init; }

    /// <summary>
    /// Gets the normalized call state the event represents.
    /// </summary>
    public VoiceCallState State { get; init; }

    /// <summary>
    /// Gets the address of the calling party, when supplied.
    /// </summary>
    public string FromAddress { get; init; }

    /// <summary>
    /// Gets the address of the called party, when supplied.
    /// </summary>
    public string ToAddress { get; init; }

    /// <summary>
    /// Gets the UTC time the event occurred. When not supplied, the current time is used.
    /// </summary>
    public DateTime? OccurredUtc { get; init; }

    /// <summary>
    /// Gets an idempotency key that uniquely identifies this provider event so duplicate
    /// deliveries can be de-duplicated. When set, replays of the same event are ignored.
    /// </summary>
    public string IdempotencyKey { get; init; }

    /// <summary>
    /// Gets an optional provider-supplied monotonic sequence number for the call stream. When
    /// supplied, ingestion uses it as the authoritative ordering high-water mark and rejects stale or
    /// equal-order deliveries. Providers that only supply timestamps or idempotency keys leave it
    /// <see langword="null"/> and ingestion falls back to timestamp-based ordering.
    /// </summary>
    public long? SequenceNumber { get; init; }

    /// <summary>
    /// Gets a value indicating whether the provider reports the call as muted.
    /// When <see langword="null"/>, the event does not change the current mute state.
    /// </summary>
    public bool? IsMuted { get; init; }

    /// <summary>
    /// Gets the provider-reported recording state.
    /// When <see langword="null"/>, the event does not change the current recording state.
    /// </summary>
    public RecordingState? RecordingState { get; init; }

    /// <summary>
    /// Gets the provider recording reference for the session, when recording is active or retained.
    /// </summary>
    public string RecordingReference { get; init; }

    /// <summary>
    /// Gets a value indicating whether the provider reports the call as a conference or
    /// multi-party session. When <see langword="null"/>, the event does not change the current conference flag.
    /// </summary>
    public bool? IsConference { get; init; }

    /// <summary>
    /// Gets the number of active participants the provider reports for the session.
    /// When <see langword="null"/>, the event does not change the current participant count.
    /// </summary>
    public int? ParticipantCount { get; init; }

    /// <summary>
    /// Gets the provider-neutral AMD (Answering Machine Detection) answer classification when the provider
    /// reports AMD for this event. When <see langword="null"/>, the provider did not report AMD and the event does
    /// not change the current answer classification.
    /// </summary>
    public AnswerClassification? AnswerClassification { get; init; }

    /// <summary>
    /// Gets the provider-neutral reason the call ended. It is required whenever <see cref="State"/>
    /// is terminal, because a call that ended for an unrecorded reason cannot be counted in outbound
    /// compliance reporting or abandon analytics. When the provider ends a call without reporting any
    /// release cause, <see cref="Telephony.Models.HangupCause.Unknown"/> records that honestly instead of
    /// presenting the call as a normal clearing.
    /// </summary>
    public HangupCause? HangupCause { get; init; }

    /// <summary>
    /// Gets additional provider metadata to retain for troubleshooting. The value is snapshotted on
    /// assignment, so a caller that keeps its own reference to the dictionary cannot change the event
    /// after it has been handed over.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata
    {
        get => _metadata;
        init => _metadata = Snapshot(value);
    }

    private static MetadataSnapshot Snapshot(IReadOnlyDictionary<string, string> value)
    {
        if (value is null || value.Count == 0)
        {
            return _emptyMetadata;
        }

        if (value is MetadataSnapshot snapshot)
        {
            return snapshot;
        }

        return new MetadataSnapshot(new Dictionary<string, string>(value, ComparerOf(value)));
    }

    private static IEqualityComparer<string> ComparerOf(IReadOnlyDictionary<string, string> value)
    {
        // The comparer is carried over wherever the source can report one, because providers key their
        // metadata case-insensitively and a snapshot that quietly became case-sensitive would change what
        // consumers can find. An implementation that reports no comparer is keyed ordinally, which is the
        // only honest choice when the source will not say how it compares its own keys.
        return value switch
        {
            MetadataSnapshot snapshot => snapshot.Comparer,
            Dictionary<string, string> dictionary => dictionary.Comparer,
            ConcurrentDictionary<string, string> dictionary => dictionary.Comparer,
            ImmutableDictionary<string, string> dictionary => dictionary.KeyComparer,
            FrozenDictionary<string, string> dictionary => dictionary.Comparer,
            _ => StringComparer.Ordinal,
        };
    }

    /// <summary>
    /// An immutable view over metadata that reports the comparer its keys are held under, so a snapshot
    /// taken from another event's <see cref="Metadata"/> keeps the comparer the provider supplied instead
    /// of silently falling back to ordinal comparison.
    /// </summary>
    private sealed class MetadataSnapshot : IReadOnlyDictionary<string, string>
    {
        private readonly Dictionary<string, string> _values;

        public MetadataSnapshot(Dictionary<string, string> values)
        {
            _values = values;
        }

        public IEqualityComparer<string> Comparer => _values.Comparer;

        public string this[string key] => _values[key];

        public IEnumerable<string> Keys => _values.Keys;

        public IEnumerable<string> Values => _values.Values;

        public int Count => _values.Count;

        public bool ContainsKey(string key) => _values.ContainsKey(key);

        public bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value);

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
