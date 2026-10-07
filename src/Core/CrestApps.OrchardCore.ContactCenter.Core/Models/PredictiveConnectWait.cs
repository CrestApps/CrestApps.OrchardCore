namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Decides how long an answered over-dialed call may wait for an agent about to free up, so the wait plus the time it takes
/// to connect an agent stays within <see cref="DialerAbandonment.ConnectThreshold"/>.
/// </summary>
/// <remarks>
/// A wait only helps when the agent it waits for can still be connected inside the two seconds after which the call counts
/// as abandoned. That depends on how long connecting an agent takes on this platform, which only the campaign's measured
/// answer-to-agent time (its 95th percentile) can say. With too few measured connects there is no evidence the wait is
/// safe, so none is allowed.
/// </remarks>
public static class PredictiveConnectWait
{
    /// <summary>
    /// The fewest measured connects the 95th percentile is trusted from.
    /// </summary>
    public const int MinimumLatencySamples = 20;

    /// <summary>
    /// Checks a profile's connect wait against the measured connect time.
    /// </summary>
    /// <param name="waitMilliseconds">The profile's connect wait.</param>
    /// <param name="statistics">The profile's measured pacing statistics, or <see langword="null"/> when there are none.</param>
    /// <returns>The verdict, with the longest wait the measurement allows.</returns>
    public static PredictiveConnectWaitCheck Check(int waitMilliseconds, DialerPacingStatistics statistics)
    {
        if (waitMilliseconds <= 0)
        {
            return new PredictiveConnectWaitCheck(PredictiveConnectWaitVerdict.NoWait, 0, null);
        }

        var p95 = statistics?.P95ConnectLatency;

        if (p95 is null || statistics.ConnectLatencySamples < MinimumLatencySamples || p95.Value < TimeSpan.Zero)
        {
            return new PredictiveConnectWaitCheck(PredictiveConnectWaitVerdict.NoLatencyData, 0, p95);
        }

        var allowed = (int)Math.Floor(Math.Max(0, (DialerAbandonment.ConnectThreshold - p95.Value).TotalMilliseconds));

        return waitMilliseconds <= allowed
            ? new PredictiveConnectWaitCheck(PredictiveConnectWaitVerdict.Allowed, allowed, p95)
            : new PredictiveConnectWaitCheck(PredictiveConnectWaitVerdict.ExceedsBudget, allowed, p95);
    }

    /// <summary>
    /// The wait the connector actually applies: the profile's wait while it fits the measured budget, the measured budget
    /// when it no longer does, and none without a measurement.
    /// </summary>
    /// <param name="waitMilliseconds">The profile's connect wait.</param>
    /// <param name="statistics">The profile's measured pacing statistics, or <see langword="null"/> when there are none.</param>
    /// <returns>The wait to apply, in milliseconds.</returns>
    public static int ResolveEffectiveMilliseconds(int waitMilliseconds, DialerPacingStatistics statistics)
    {
        var check = Check(waitMilliseconds, statistics);

        return check.Verdict switch
        {
            PredictiveConnectWaitVerdict.Allowed => waitMilliseconds,
            PredictiveConnectWaitVerdict.ExceedsBudget => check.MaxAllowedMilliseconds,
            _ => 0,
        };
    }
}
