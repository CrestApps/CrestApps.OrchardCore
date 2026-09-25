namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// The provider's own measurement of one call leg, as it reports it when the leg ends.
/// </summary>
/// <remarks>
/// The browser can only measure the legs it holds, so an agent's soft phone says nothing about the customer's phone,
/// a hidden bridge leg, or an automated call. The provider measures every leg it carries, which is what makes the
/// customer's side of a poor call visible at all. Counts are packets. A skipped packet is a playout slot the provider
/// had nothing to play in: a packet lost in transit leaves one, but so do silence suppression, a greeting or hold music
/// playing to a caller who says nothing, and a party that sends nothing at all, so a skipped count is not a loss count.
/// </remarks>
public sealed class ProviderCallQualityStats
{
    /// <summary>
    /// Gets or sets the provider's mean opinion score for the audio it received on this leg, or <see langword="null"/>
    /// when it reported none.
    /// </summary>
    public double? InboundMos { get; set; }

    /// <summary>
    /// Gets or sets the largest jitter variance the provider saw on the audio it received, as it reports it
    /// (<c>jitter_max_variance</c>).
    /// </summary>
    /// <remarks>
    /// This is a peak variance of packet arrival over the whole leg, not the mean jitter a soft phone reports, so it is
    /// kept for diagnosis and never compared with a jitter threshold.
    /// </remarks>
    public double? InboundJitterMaxVarianceMs { get; set; }

    /// <summary>
    /// Gets or sets the number of packets the provider used to measure jitter on the audio it received.
    /// </summary>
    public long? InboundJitterPacketCount { get; set; }

    /// <summary>
    /// Gets or sets the number of audio packets the provider received on this leg.
    /// </summary>
    public long? InboundPacketCount { get; set; }

    /// <summary>
    /// Gets or sets the number of playout slots on this leg the provider had no received packet for.
    /// </summary>
    public long? InboundSkipPacketCount { get; set; }

    /// <summary>
    /// Gets or sets the number of audio packets the provider sent on this leg.
    /// </summary>
    public long? OutboundPacketCount { get; set; }

    /// <summary>
    /// Gets or sets the number of audio packets the provider skipped sending on this leg.
    /// </summary>
    public long? OutboundSkipPacketCount { get; set; }

    /// <summary>
    /// Gets the share of this leg's playout slots the provider had no received packet for, as a percentage, or
    /// <see langword="null"/> when there is nothing to measure it from.
    /// </summary>
    /// <remarks>
    /// It is not packet loss and nothing is rated on it; see <see cref="InboundSkipPacketCount"/>.
    /// </remarks>
    public double? InboundSkippedPercent
        => InboundPacketCount is > 0 || InboundSkipPacketCount is > 0
            ? 100.0 * (InboundSkipPacketCount ?? 0) / ((InboundPacketCount ?? 0) + (InboundSkipPacketCount ?? 0))
            : null;

    /// <summary>
    /// Gets the provider's opinion score for the audio it received, or <see langword="null"/> when it received no audio
    /// packets to score: a score of nothing says nothing about the leg.
    /// </summary>
    public double? MeasuredInboundMos
        => InboundPacketCount is 0 ? null : InboundMos;

    /// <summary>
    /// Gets whether the provider reported anything worth keeping.
    /// </summary>
    public bool HasMeasurements
        => InboundMos.HasValue ||
            InboundPacketCount.HasValue ||
            InboundSkipPacketCount.HasValue ||
            OutboundPacketCount.HasValue ||
            OutboundSkipPacketCount.HasValue;
}
