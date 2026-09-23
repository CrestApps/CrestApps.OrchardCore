using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telnyx.Services;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Telnyx measures every leg it carries and reports it on the hangup, which is the only view of the customer's side
/// of a call. It sends every figure as a string, so a parser that expected numbers would drop all of it silently.
/// </summary>
public sealed class TelnyxCallQualityStatsParserTests
{
    [Fact]
    public void Parse_HangupWithQualityStats_ReadsBothSides()
    {
        // Arrange
        const string Payload = """
        {
          "data": {
            "event_type": "call.hangup",
            "payload": {
              "call_control_id": "ctrl-1",
              "hangup_cause": "normal_clearing",
              "call_quality_stats": {
                "inbound": {
                  "jitter_max_variance": "12.5",
                  "jitter_packet_count": "0",
                  "mos": "4.38",
                  "packet_count": "950",
                  "skip_packet_count": "50"
                },
                "outbound": {
                  "packet_count": "1002",
                  "skip_packet_count": "3"
                }
              }
            }
          }
        }
        """;

        // Act
        var parsed = TelnyxCallEventParser.TryParse(Payload, out var callEvent);

        // Assert
        Assert.True(parsed);
        var stats = callEvent.CallQualityStats;
        Assert.NotNull(stats);
        Assert.Equal(4.38, stats.InboundMos);
        Assert.Equal(12.5, stats.InboundJitterMaxVarianceMs);
        Assert.Equal(0, stats.InboundJitterPacketCount);
        Assert.Equal(950, stats.InboundPacketCount);
        Assert.Equal(50, stats.InboundSkipPacketCount);
        Assert.Equal(1002, stats.OutboundPacketCount);
        Assert.Equal(3, stats.OutboundSkipPacketCount);
        Assert.Equal(5.0, stats.InboundLossPercent);
    }

    [Fact]
    public void Parse_HangupWithoutQualityStats_LeavesThemUnset()
    {
        // Arrange
        const string Payload = """
        { "data": { "event_type": "call.hangup", "payload": { "call_control_id": "ctrl-1", "call_quality_stats": { "inbound": { "mos": "n/a" } } } } }
        """;

        // Act
        var parsed = TelnyxCallEventParser.TryParse(Payload, out var callEvent);

        // Assert
        Assert.True(parsed);
        Assert.Null(callEvent.CallQualityStats);
    }

    [Fact]
    public void Parse_StreamingFailed_ReadsTheFailureReason()
    {
        // Arrange
        const string Payload = """
        { "data": { "event_type": "streaming.failed", "payload": { "call_control_id": "ctrl-1", "failure_reason": "connection_failed" } } }
        """;

        // Act
        var parsed = TelnyxCallEventParser.TryParse(Payload, out var callEvent);

        // Assert
        Assert.True(parsed);
        Assert.Equal("connection_failed", callEvent.FailureReason);
    }

    [Fact]
    public void Evaluate_ProviderFigures_RateOnWhicheverIsKnown()
    {
        // Act & Assert
        Assert.Equal(CallQualityRating.Good, TelephonyCallQualityEvaluator.Evaluate(4.4, 0.5));
        Assert.Equal(CallQualityRating.Degraded, TelephonyCallQualityEvaluator.Evaluate(null, 2.5));
        Assert.Equal(CallQualityRating.Poor, TelephonyCallQualityEvaluator.Evaluate(3.2, null));
        Assert.Equal(CallQualityRating.Good, TelephonyCallQualityEvaluator.Evaluate(null, null));
    }

    [Fact]
    public void EvaluateSummary_ACallThatRecoveredAtTheEnd_IsStillRatedOnItsAverage()
    {
        // Arrange: the last sample was clean, the call as a whole was not.
        var summary = new CallQualityReport { Final = true, Mos = 4.4, LossPercent = 0, AvgMos = 3.3, MaxLossPercent = 9, PacketsReceived = 100, BytesReceived = 1000 };

        // Act
        var rating = TelephonyCallQualityEvaluator.EvaluateSummary(summary);

        // Assert
        Assert.Equal(CallQualityRating.Poor, rating);
    }
}
