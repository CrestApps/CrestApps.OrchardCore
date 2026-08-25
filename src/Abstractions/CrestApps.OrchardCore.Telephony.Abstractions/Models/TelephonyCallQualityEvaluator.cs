namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// A server-side rating of a <see cref="CallQualityReport"/>.
/// </summary>
public enum CallQualityRating
{
    /// <summary>
    /// The call quality is acceptable.
    /// </summary>
    Good,

    /// <summary>
    /// The call quality is noticeably reduced but still usable.
    /// </summary>
    Degraded,

    /// <summary>
    /// The call quality is poor (audible problems, or broken inbound media).
    /// </summary>
    Poor,
}

/// <summary>
/// Classifies a <see cref="CallQualityReport"/> into a <see cref="CallQualityRating"/> from its measured
/// metrics. The server rates the report independently of the browser's own <see cref="CallQualityReport.Poor"/>
/// flag so alerting does not depend on trusting a client value, and so the thresholds live in one place that
/// unit tests can pin.
/// </summary>
public static class TelephonyCallQualityEvaluator
{
    /// <summary>
    /// The MOS at or below which a sample is rated <see cref="CallQualityRating.Poor"/>.
    /// </summary>
    public const double PoorMosThreshold = 3.5;

    /// <summary>
    /// The interval loss percentage at or above which a sample is rated <see cref="CallQualityRating.Poor"/>.
    /// </summary>
    public const double PoorLossPercentThreshold = 5.0;

    /// <summary>
    /// The MOS at or below which a sample is rated <see cref="CallQualityRating.Degraded"/>.
    /// </summary>
    public const double DegradedMosThreshold = 4.0;

    /// <summary>
    /// The interval loss percentage at or above which a sample is rated <see cref="CallQualityRating.Degraded"/>.
    /// </summary>
    public const double DegradedLossPercentThreshold = 2.0;

    /// <summary>
    /// Rates a call-quality report.
    /// </summary>
    /// <param name="report">The reported metrics.</param>
    /// <returns>The rating.</returns>
    public static CallQualityRating Evaluate(CallQualityReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        // Broken inbound media: the connection carried packets but no audio bytes are arriving. Only treat a
        // zero byte count as broken once the call has actually received packets, so the first sample of a call
        // that is still coming up is not misreported as poor.
        if (report.BytesReceived == 0 && report.PacketsReceived > 0)
        {
            return CallQualityRating.Poor;
        }

        if (report.Mos > 0 && report.Mos <= PoorMosThreshold)
        {
            return CallQualityRating.Poor;
        }

        if (report.LossPercent >= PoorLossPercentThreshold)
        {
            return CallQualityRating.Poor;
        }

        if ((report.Mos > 0 && report.Mos <= DegradedMosThreshold) ||
            report.LossPercent >= DegradedLossPercentThreshold)
        {
            return CallQualityRating.Degraded;
        }

        return CallQualityRating.Good;
    }
}
