namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// The provider's own measurement of one call leg, as it reports it when the leg ends.
/// </summary>
/// <remarks>
/// The browser can only measure the legs it holds, so an agent's soft phone says nothing about the customer's phone,
/// a hidden bridge leg, or an automated call. The provider measures every leg it carries, which is what makes the
/// customer's side of a poor call visible at all. Counts are packets; a skipped packet is one the provider did not
/// receive in time to play.
/// </remarks>
public sealed class ProviderCallQualityStats
{
    /// <summary>
    /// Gets or sets the provider's mean opinion score for the audio it received on this leg, or <see langword="null"/>
    /// when it reported none.
    /// </summary>
    public double? InboundMos { get; set; }

    /// <summary>
    /// Gets or sets the largest jitter variance the provider saw on the audio it received, in milliseconds.
    /// </summary>
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
    /// Gets or sets the number of audio packets the provider expected on this leg and did not receive.
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
    /// Gets the share of the audio the provider expected on this leg and did not receive, as a percentage, or
    /// <see langword="null"/> when there is nothing to measure it from.
    /// </summary>
    public double? InboundLossPercent
        => InboundPacketCount is > 0 || InboundSkipPacketCount is > 0
            ? 100.0 * (InboundSkipPacketCount ?? 0) / ((InboundPacketCount ?? 0) + (InboundSkipPacketCount ?? 0))
            : null;

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
