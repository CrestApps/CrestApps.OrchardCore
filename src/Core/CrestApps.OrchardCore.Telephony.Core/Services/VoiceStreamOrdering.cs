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
    /// Determines whether the specified delivery must be discarded rather than applied to the stream.
    /// </summary>
    /// <param name="watermark">What the consumer has already applied for this stream.</param>
    /// <param name="delivery">The incoming normalized delivery.</param>
    /// <returns>
    /// <see langword="true"/> when the delivery regresses the lifecycle, repeats or precedes an established
    /// sequence domain, or predates the last applied delivery; otherwise <see langword="false"/>.
    /// </returns>
    public static bool ShouldDiscard(VoiceStreamWatermark watermark, VoiceStreamDelivery delivery)
    {
        ArgumentNullException.ThrowIfNull(watermark);
        ArgumentNullException.ThrowIfNull(delivery);

        if (watermark.Phase == VoiceCallLifecyclePhase.Terminal ||
            delivery.Phase < watermark.Phase)
        {
            return true;
        }

        // A terminal delivery is never stale. Provider nodes do not share a clock and do not all stamp
        // sequence numbers, so a hangup can carry a timestamp behind the state change that preceded it or
        // arrive unsequenced after a sequenced delivery. Applying the staleness guards to it would discard
        // the only notification that the call is over, stranding the stream in a live phase forever.
        // Ending twice is not a risk, because an already terminal stream is rejected above and an exact
        // redelivery is rejected by the ingress duplicate check before ordering runs.
        if (delivery.Phase == VoiceCallLifecyclePhase.Terminal)
        {
            return false;
        }

        // Once a provider establishes a sequence domain, unsequenced deliveries cannot safely advance it.
        if (watermark.HighWaterSequence.HasValue)
        {
            if (!delivery.SequenceNumber.HasValue ||
                delivery.SequenceNumber.Value <= watermark.HighWaterSequence.Value)
            {
                return true;
            }
        }

        return watermark.LastEventUtc.HasValue &&
            delivery.OccurredUtc < watermark.LastEventUtc.Value;
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
