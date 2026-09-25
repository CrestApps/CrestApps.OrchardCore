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

    /// <summary>
    /// Rates a whole call from its end-of-call summary.
    /// </summary>
    /// <remarks>
    /// The summary carries the call's last sample alongside its averages, and a call is not judged by its last few
    /// seconds: one that was poor throughout and recovered as it ended would otherwise be recorded as good. The
    /// average opinion score and the worst loss are what it is rated on, falling back to the last sample only when
    /// the averages were never measured.
    /// </remarks>
    /// <param name="report">The end-of-call summary.</param>
    /// <returns>The rating.</returns>
    public static CallQualityRating EvaluateSummary(CallQualityReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (report.BytesReceived == 0 && report.PacketsReceived > 0)
        {
            return CallQualityRating.Poor;
        }

        var mos = report.AvgMos > 0 ? report.AvgMos : report.Mos;
        var loss = Math.Max(report.MaxLossPercent, report.LossPercent);

        return Evaluate(mos > 0 ? mos : null, loss);
    }

    /// <summary>
    /// Rates a leg from the provider's own measurement of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The leg is rated on the provider's opinion score alone, which already accounts for packets lost in transit and
    /// for late arrivals. The skipped-packet counts are not loss: a slot is skipped whenever there was nothing to play,
    /// which is what silence suppression, a greeting or hold music playing to a caller who says nothing, and a party
    /// that sends nothing at all all look like. Live voicemail and agent legs skipped from 1% to every slot while the
    /// provider scored them at its 4.5 maximum, so rating on them called clean calls poor.
    /// </para>
    /// <para>
    /// The jitter figure the provider reports is a peak variance of packet arrival, not a mean jitter, so it is not
    /// rated against a jitter threshold either. Both are kept on the raw statistics for diagnosis.
    /// </para>
    /// </remarks>
    /// <param name="stats">The provider's statistics for the leg.</param>
    /// <returns>The rating.</returns>
    public static CallQualityRating EvaluateProvider(ProviderCallQualityStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);

        return Evaluate(stats.MeasuredInboundMos, lossPercent: null);
    }

    /// <summary>
    /// Rates a leg from an opinion score and a loss percentage, either of which may be unknown.
    /// </summary>
    /// <param name="mos">The mean opinion score, or <see langword="null"/> when not measured.</param>
    /// <param name="lossPercent">The packet loss percentage, or <see langword="null"/> when not measured.</param>
    /// <returns>The rating.</returns>
    public static CallQualityRating Evaluate(double? mos, double? lossPercent)
    {
        if ((mos is > 0 and <= PoorMosThreshold) || lossPercent >= PoorLossPercentThreshold)
        {
            return CallQualityRating.Poor;
        }

        if ((mos is > 0 and <= DegradedMosThreshold) || lossPercent >= DegradedLossPercentThreshold)
        {
            return CallQualityRating.Degraded;
        }

        return CallQualityRating.Good;
    }
}
