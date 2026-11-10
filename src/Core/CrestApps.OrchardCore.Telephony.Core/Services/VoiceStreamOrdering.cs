using CrestApps.OrchardCore.Telephony.Core.Models;

namespace CrestApps.OrchardCore.Telephony.Core.Services;

/// <summary>
/// Decides whether a normalized provider delivery advances a call stream or must be discarded as stale.
/// These rules are provider-neutral and consumer-neutral on purpose: every projection built on the same
/// provider stream has to reach the same decision, because two projections that disagree about which
/// deliveries are stale will disagree about the state of the same call.
/// </summary>
public static class VoiceStreamOrdering
{
    /// <summary>
    /// The stream had already ended.
    /// </summary>
    public const string DiscardAfterTerminal = "after_terminal";

    /// <summary>
    /// The delivery would move the call back to an earlier phase.
    /// </summary>
    public const string DiscardPhaseRegression = "phase_regression";

    /// <summary>
    /// The delivery carries no sequence number on a stream whose provider numbers its deliveries.
    /// </summary>
    public const string DiscardUnsequenced = "unsequenced";

    /// <summary>
    /// The delivery repeats or precedes a sequence number already applied.
    /// </summary>
    public const string DiscardSequenceRegression = "sequence_regression";

    /// <summary>
    /// The delivery happened before the last one applied.
    /// </summary>
    public const string DiscardOutOfOrder = "out_of_order";

    /// <summary>
    /// Determines whether the specified delivery must be discarded rather than applied to the stream.
    /// </summary>
    /// <param name="watermark">What the consumer has already applied for this stream.</param>
    /// <param name="delivery">The incoming normalized delivery.</param>
    /// <returns>
    /// <see langword="true"/> when the delivery regresses the lifecycle, repeats or precedes an established
    /// sequence domain, or predates the last applied delivery; otherwise <see langword="false"/>.
    /// </returns>
    public static bool ShouldDiscard(VoiceStreamWatermark watermark, VoiceStreamDelivery delivery)
        => GetDiscardReason(watermark, delivery) is not null;

    /// <summary>
    /// Gets why the specified delivery must be discarded rather than applied to the stream, as a bounded value for
    /// the <c>reason</c> tag of <c>crestapps.telephony.voice_event.discards</c>.
    /// </summary>
    /// <param name="watermark">What the consumer has already applied for this stream.</param>
    /// <param name="delivery">The incoming normalized delivery.</param>
    /// <returns>
    /// <see cref="DiscardAfterTerminal"/>, <see cref="DiscardPhaseRegression"/>, <see cref="DiscardUnsequenced"/>,
    /// <see cref="DiscardSequenceRegression"/> or <see cref="DiscardOutOfOrder"/>; <see langword="null"/> when the
    /// delivery is applied.
    /// </returns>
    public static string GetDiscardReason(VoiceStreamWatermark watermark, VoiceStreamDelivery delivery)
    {
        ArgumentNullException.ThrowIfNull(watermark);
        ArgumentNullException.ThrowIfNull(delivery);

        if (watermark.Phase == VoiceCallLifecyclePhase.Terminal)
        {
            return DiscardAfterTerminal;
        }

        if (delivery.Phase < watermark.Phase)
        {
            return DiscardPhaseRegression;
        }

        // A terminal delivery is never stale. Provider nodes do not share a clock and do not all stamp
        // sequence numbers, so a hangup can carry a timestamp behind the state change that preceded it or
        // arrive unsequenced after a sequenced delivery. Applying the staleness guards to it would discard
        // the only notification that the call is over, stranding the stream in a live phase forever.
        // Ending twice is not a risk, because an already terminal stream is rejected above and an exact
        // redelivery is rejected by the ingress duplicate check before ordering runs.
        if (delivery.Phase == VoiceCallLifecyclePhase.Terminal)
        {
            return null;
        }

        // Once a provider establishes a sequence domain, unsequenced deliveries cannot safely advance it.
        if (watermark.HighWaterSequence.HasValue)
        {
            if (!delivery.SequenceNumber.HasValue)
            {
                return DiscardUnsequenced;
            }

            if (delivery.SequenceNumber.Value <= watermark.HighWaterSequence.Value)
            {
                return DiscardSequenceRegression;
            }
        }

        return watermark.LastEventUtc.HasValue && delivery.OccurredUtc < watermark.LastEventUtc.Value
            ? DiscardOutOfOrder
            : null;
    }

    /// <summary>
    /// Advances the specified watermark with a delivery that has been accepted. The watermark never moves
    /// backwards, because a rewound watermark would re-admit deliveries the stream has already discarded.
    /// </summary>
    /// <param name="watermark">The watermark to advance.</param>
    /// <param name="delivery">The accepted normalized delivery.</param>
    public static void Advance(VoiceStreamWatermark watermark, VoiceStreamDelivery delivery)
    {
        ArgumentNullException.ThrowIfNull(watermark);
        ArgumentNullException.ThrowIfNull(delivery);

        watermark.Phase = delivery.Phase > watermark.Phase
            ? delivery.Phase
            : watermark.Phase;

        watermark.LastEventUtc = watermark.LastEventUtc.HasValue && watermark.LastEventUtc.Value > delivery.OccurredUtc
            ? watermark.LastEventUtc.Value
            : delivery.OccurredUtc;

        if (delivery.SequenceNumber.HasValue)
        {
            watermark.HighWaterSequence = watermark.HighWaterSequence.HasValue
                ? Math.Max(watermark.HighWaterSequence.Value, delivery.SequenceNumber.Value)
                : delivery.SequenceNumber.Value;
        }
    }
}
