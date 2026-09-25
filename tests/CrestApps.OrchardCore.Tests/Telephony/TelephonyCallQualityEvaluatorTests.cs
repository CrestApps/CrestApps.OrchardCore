using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class TelephonyCallQualityEvaluatorTests
{
    [Fact]
    public void Evaluate_ForHealthyMetrics_RatesGood()
    {
        var report = new CallQualityReport
        {
            Mos = 4.3,
            LossPercent = 0.5,
            PacketsReceived = 500,
            BytesReceived = 80_000,
        };

        Assert.Equal(CallQualityRating.Good, TelephonyCallQualityEvaluator.Evaluate(report));
    }

    [Theory]
    [InlineData(3.7, 3.0)] // MOS below the degraded threshold.
    [InlineData(4.2, 3.0)] // Loss at/above the degraded threshold but MOS fine.
    public void Evaluate_ForModeratelyReducedQuality_RatesDegraded(double mos, double lossPercent)
    {
        var report = new CallQualityReport
        {
            Mos = mos,
            LossPercent = lossPercent,
            PacketsReceived = 500,
            BytesReceived = 80_000,
        };

        Assert.Equal(CallQualityRating.Degraded, TelephonyCallQualityEvaluator.Evaluate(report));
    }

    [Theory]
    [InlineData(3.2, 1.0)] // MOS at/below the poor threshold.
    [InlineData(4.2, 6.0)] // Loss at/above the poor threshold.
    public void Evaluate_ForBadMetrics_RatesPoor(double mos, double lossPercent)
    {
        var report = new CallQualityReport
        {
            Mos = mos,
            LossPercent = lossPercent,
            PacketsReceived = 500,
            BytesReceived = 80_000,
        };

        Assert.Equal(CallQualityRating.Poor, TelephonyCallQualityEvaluator.Evaluate(report));
    }

    [Fact]
    public void Evaluate_WhenPacketsArriveButNoBytes_RatesPoorAsBrokenInboundMedia()
    {
        // Packets are being received but zero audio bytes are arriving: the classic one-way-audio symptom the
        // TURN regression produced. This must be flagged even when MOS and loss look fine.
        var report = new CallQualityReport
        {
            Mos = 4.4,
            LossPercent = 0,
            PacketsReceived = 300,
            BytesReceived = 0,
        };

        Assert.Equal(CallQualityRating.Poor, TelephonyCallQualityEvaluator.Evaluate(report));
    }

    [Fact]
    public void Evaluate_ForFirstSampleBeforeAnyPackets_DoesNotMisreportBrokenMedia()
    {
        // A brand-new call that has not yet received any packets has zero bytes too; that is start-up, not
        // broken media, so it must not be rated poor on the byte count alone.
        var report = new CallQualityReport
        {
            Mos = 4.4,
            LossPercent = 0,
            PacketsReceived = 0,
            BytesReceived = 0,
        };

        Assert.Equal(CallQualityRating.Good, TelephonyCallQualityEvaluator.Evaluate(report));
    }

    [Fact]
    public void Evaluate_WhenMosIsUnset_DoesNotTreatZeroAsPoor()
    {
        // A report that carries no MOS (0) but otherwise healthy metrics must not be dragged to poor by the
        // MOS check, which only applies to a positive score.
        var report = new CallQualityReport
        {
            Mos = 0,
            LossPercent = 0,
            PacketsReceived = 500,
            BytesReceived = 80_000,
        };

        Assert.Equal(CallQualityRating.Good, TelephonyCallQualityEvaluator.Evaluate(report));
    }

    // The live summary of a one-way call: the agent heard the caller perfectly (a 4.37 opinion score, no loss) while the
    // soft phone's sender, carrying a track that had ended, sent nothing at all. It was rated Good.
    [Fact]
    public void EvaluateSummary_ALegThatReceivedButSentNothing_IsPoor()
    {
        // Arrange
        var summary = new CallQualityReport
        {
            Final = true,
            Mos = 4.3672791040000005,
            AvgMos = 4.3672791040000005,
            PacketsReceived = 590,
            BytesReceived = 18936,
            SentTrackReported = true,
            SentTrackIsLocalStream = true,
            BytesSent = 0,
            PacketsSent = 0,
            DurationMs = 12129,
        };

        // Act
        var rating = TelephonyCallQualityEvaluator.EvaluateSummary(summary);

        // Assert
        Assert.Equal(CallQualityRating.Poor, rating);
        Assert.True(TelephonyCallQualityEvaluator.SentNoAudio(summary));
    }

    [Fact]
    public void EvaluateSummary_ALegOnWhichTheSoftPhoneSawAudioStopLeaving_IsPoor()
    {
        // Arrange: audio stopped leaving for a stretch mid-call, and the totals alone look healthy.
        var summary = new CallQualityReport
        {
            Final = true,
            AvgMos = 4.4,
            PacketsReceived = 3000,
            BytesReceived = 480_000,
            SentTrackReported = true,
            PacketsSent = 1200,
            BytesSent = 190_000,
            OutboundAudioStalled = true,
        };

        // Act
        var rating = TelephonyCallQualityEvaluator.EvaluateSummary(summary);

        // Assert
        Assert.Equal(CallQualityRating.Poor, rating);
    }

    [Fact]
    public void Evaluate_ASampleThatReceivedButSentNothing_IsPoor()
    {
        // Arrange
        var sample = new CallQualityReport
        {
            Mos = 4.4,
            PacketsReceived = 400,
            BytesReceived = 64_000,
            SentTrackReported = true,
            PacketsSent = 0,
        };

        // Act & Assert
        Assert.Equal(CallQualityRating.Poor, TelephonyCallQualityEvaluator.Evaluate(sample));
    }

    // Nothing has moved in either direction yet, or the browser never said what it was sending: not evidence of a
    // one-way call.
    [Theory]
    [InlineData(true, 0L)]
    [InlineData(false, 400L)]
    public void Evaluate_WithoutEvidenceOfNothingSent_IsNotRatedOnIt(bool sentTrackReported, long packetsReceived)
    {
        // Arrange
        var sample = new CallQualityReport
        {
            Mos = 4.4,
            PacketsReceived = packetsReceived,
            BytesReceived = packetsReceived * 160,
            SentTrackReported = sentTrackReported,
            PacketsSent = 0,
        };

        // Act & Assert
        Assert.Equal(CallQualityRating.Good, TelephonyCallQualityEvaluator.Evaluate(sample));
        Assert.False(TelephonyCallQualityEvaluator.SentNoAudio(sample));
    }
}
