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
}
