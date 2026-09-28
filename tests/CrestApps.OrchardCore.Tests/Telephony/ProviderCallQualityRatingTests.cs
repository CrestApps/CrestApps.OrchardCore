using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telnyx.Services;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Telnyx's hangup statistics count a "skipped" packet whenever a playout slot had nothing to play: silence
/// suppression, a greeting or hold music the platform was sending while the caller said nothing, or a party that sent
/// nothing at all. Reading those counts as network loss rated voicemail calls and clean agent legs degraded or poor while
/// Telnyx's own opinion score, which does account for packets lost in transit, sat at its 4.5 maximum. The figures here
/// are the live legs that were misrated.
/// </summary>
public sealed class ProviderCallQualityRatingTests
{
    public static TheoryData<string, double, double, long, long, long> MisratedLiveLegs()
        => new()
        {
            // The customer's leg of a call sent to voicemail: the greeting played while the caller listened.
            { "voicemail customer leg (was Poor)", 4.5, 0, 133, 11, 116 },
            { "voicemail customer leg (was Degraded)", 4.5, 0, 642, 22, 344 },

            // An agent's answered leg: 170 of 5,157 slots skipped, and the provider's score at its maximum.
            { "agent leg, skipped 170 of 5157 (was Degraded)", 4.5, 1206.07, 5157, 170, 4304 },

            // An agent's leg whose jitter figure is the provider's peak variance, not a mean jitter.
            { "agent leg, jitter max variance 852.04 (was Degraded)", 4.5, 852.04, 1557, 75, 1585 },

            // An agent's leg on which the provider received no packets at all from the soft phone.
            { "agent leg, nothing received (was Poor at 100% loss)", 4.5, 0, 0, 596, 572 },
        };

    [Theory]
    [MemberData(nameof(MisratedLiveLegs))]
    public void EvaluateProvider_SkippedPacketsWithAPerfectOpinionScore_AreNotRatedAsLoss(
        string leg,
        double mos,
        double jitterMaxVariance,
        long inboundPackets,
        long inboundSkipped,
        long outboundPackets)
    {
        // Arrange
        var stats = Parse(mos, jitterMaxVariance, inboundPackets, inboundSkipped, outboundPackets);

        // Act
        var rating = TelephonyCallQualityEvaluator.EvaluateProvider(stats);

        // Assert
        Assert.True(rating == CallQualityRating.Good, $"The {leg} rated {rating}.");
    }

    [Fact]
    public void EvaluateProvider_ALowOpinionScore_IsStillPoor()
    {
        // Arrange: the provider's score falls when packets are really lost or arrive too late.
        var stats = Parse(mos: 3.1, jitterMaxVariance: 40, inboundPackets: 4000, inboundSkipped: 10, outboundPackets: 4000);

        // Act
        var rating = TelephonyCallQualityEvaluator.EvaluateProvider(stats);

        // Assert
        Assert.Equal(CallQualityRating.Poor, rating);
    }

    [Fact]
    public void EvaluateProvider_AMiddlingOpinionScore_IsDegraded()
    {
        // Arrange
        var stats = Parse(mos: 3.9, jitterMaxVariance: 40, inboundPackets: 4000, inboundSkipped: 10, outboundPackets: 4000);

        // Act
        var rating = TelephonyCallQualityEvaluator.EvaluateProvider(stats);

        // Assert
        Assert.Equal(CallQualityRating.Degraded, rating);
    }

    [Fact]
    public void EvaluateSummary_TheLiveSoftPhoneCallWithAnEightHundredMillisecondRoundTrip_StaysPoor()
    {
        // Arrange: the soft phone's summary of the agent's leg of a call that really was poor.
        var summary = new CallQualityReport
        {
            Final = true,
            Mos = 1.102312,
            AvgMos = 2.7197708697421876,
            MinMos = 1.102312,
            LossPercent = 0,
            MaxLossPercent = 0,
            RoundTripTimeMs = 892,
            PacketsReceived = 1544,
            BytesReceived = 94334,
            DurationMs = 32782,
        };

        // Act
        var rating = TelephonyCallQualityEvaluator.EvaluateSummary(summary);

        // Assert
        Assert.Equal(CallQualityRating.Poor, rating);
    }

    private static ProviderCallQualityStats Parse(double mos, double jitterMaxVariance, long inboundPackets, long inboundSkipped, long outboundPackets)
    {
        // Telnyx sends every figure as a string.
        var payload = $$"""
        {
          "data": {
            "event_type": "call.hangup",
            "payload": {
              "call_control_id": "ctrl-1",
              "call_quality_stats": {
                "inbound": {
                  "jitter_max_variance": "{{jitterMaxVariance.ToString(System.Globalization.CultureInfo.InvariantCulture)}}",
                  "jitter_packet_count": "0",
                  "mos": "{{mos.ToString(System.Globalization.CultureInfo.InvariantCulture)}}",
                  "packet_count": "{{inboundPackets}}",
                  "skip_packet_count": "{{inboundSkipped}}"
                },
                "outbound": {
                  "packet_count": "{{outboundPackets}}",
                  "skip_packet_count": "0"
                }
              }
            }
          }
        }
        """;

        Assert.True(TelnyxCallEventParser.TryParse(payload, out var callEvent));

        return callEvent.CallQualityStats;
    }
}
