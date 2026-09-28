namespace CrestApps.OrchardCore.Telephony.Core.Models;

/// <summary>
/// Describes what a consumer has already applied for one provider call stream. The ingress ordering rules
/// compare an incoming delivery against this watermark to decide whether the delivery moves the stream
/// forward or is a duplicate, a reordering, or a late arrival that must be discarded.
/// </summary>
public sealed class VoiceStreamWatermark
{
    /// <summary>
    /// Gets or sets the lifecycle phase the stream has already reached.
    /// </summary>
    public VoiceCallLifecyclePhase Phase { get; set; }

    /// <summary>
    /// Gets or sets the highest provider sequence number applied to the stream, when the provider stamps
    /// sequence numbers. Once a value is present the provider has established a sequence domain, and an
    /// unsequenced delivery can no longer be trusted to advance the stream.
    /// </summary>
    public long? HighWaterSequence { get; set; }

    /// <summary>
    /// Gets or sets the time of the most recent delivery applied to the stream, in UTC.
    /// </summary>
    public DateTime? LastEventUtc { get; set; }
}
