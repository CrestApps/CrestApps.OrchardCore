using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// A connect wait only helps while the agent it waits for can still be connected inside the two seconds after which the
/// call counts as abandoned. It is allowed only when the measured 95th percentile connect time plus the wait fits, and not
/// at all without enough measured connects.
/// </summary>
public sealed class PredictiveConnectWaitTests
{
    [Fact]
    public void Check_NoWait_NeedsNoMeasurement()
    {
        // Act
        var check = PredictiveConnectWait.Check(0, statistics: null);

        // Assert
        Assert.Equal(PredictiveConnectWaitVerdict.NoWait, check.Verdict);
        Assert.Equal(0, PredictiveConnectWait.ResolveEffectiveMilliseconds(0, statistics: null));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(true, PredictiveConnectWait.MinimumLatencySamples - 1)]
    public void Check_WithoutEnoughMeasuredConnects_RefusesAnyWait(bool measured, int samples)
    {
        // Arrange
        var statistics = measured
            ? new DialerPacingStatistics { P95ConnectLatency = TimeSpan.FromMilliseconds(300), ConnectLatencySamples = samples }
            : new DialerPacingStatistics();

        // Act
        var check = PredictiveConnectWait.Check(500, statistics);

        // Assert
        Assert.Equal(PredictiveConnectWaitVerdict.NoLatencyData, check.Verdict);
        Assert.Equal(0, PredictiveConnectWait.ResolveEffectiveMilliseconds(500, statistics));
    }

    [Theory]
    [InlineData(500, 1500, PredictiveConnectWaitVerdict.Allowed, 1500)]
    [InlineData(500, 1000, PredictiveConnectWaitVerdict.Allowed, 1000)]
    [InlineData(600, 1500, PredictiveConnectWaitVerdict.ExceedsBudget, 1400)]
    [InlineData(2500, 100, PredictiveConnectWaitVerdict.ExceedsBudget, 0)]
    public void Check_WaitPlusTheMeasuredConnectTime_MustStayWithinTwoSeconds(int p95Milliseconds, int waitMilliseconds, PredictiveConnectWaitVerdict verdict, int effective)
    {
        // Arrange
        var statistics = new DialerPacingStatistics
        {
            P95ConnectLatency = TimeSpan.FromMilliseconds(p95Milliseconds),
            ConnectLatencySamples = PredictiveConnectWait.MinimumLatencySamples,
        };

        // Act
        var check = PredictiveConnectWait.Check(waitMilliseconds, statistics);

        // Assert: a wait over the budget is cut to it at run time, never stretched past two seconds.
        Assert.Equal(verdict, check.Verdict);
        Assert.Equal(Math.Max(0, 2000 - p95Milliseconds), check.MaxAllowedMilliseconds);
        Assert.Equal(effective, PredictiveConnectWait.ResolveEffectiveMilliseconds(waitMilliseconds, statistics));
    }
}
