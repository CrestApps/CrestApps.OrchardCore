namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the outcome of evaluating a dialer profile against its rolling abandonment-rate cap. The
/// evaluation is auditable: it always carries whether dialing is permitted, whether the statistics were
/// available, the measured rate, and the sample size behind the decision.
/// </summary>
public sealed class DialerAbandonmentEvaluation
{
    private DialerAbandonmentEvaluation(
        bool isPermitted,
        bool statisticsAvailable,
        double ratePercent,
        long sampleSize,
        string description)
    {
        IsPermitted = isPermitted;
        StatisticsAvailable = statisticsAvailable;
        RatePercent = ratePercent;
        SampleSize = sampleSize;
        Description = description;
    }

    /// <summary>
    /// Gets a value indicating whether outbound dialing is permitted under the abandonment policy.
    /// </summary>
    public bool IsPermitted { get; }

    /// <summary>
    /// Gets a value indicating whether the rolling abandonment statistics were available for the decision.
    /// </summary>
    public bool StatisticsAvailable { get; }

    /// <summary>
    /// Gets the measured rolling abandonment rate, expressed as a percentage of live-answered calls.
    /// </summary>
    public double RatePercent { get; }

    /// <summary>
    /// Gets the number of live-answered calls the decision was measured against.
    /// </summary>
    public long SampleSize { get; }

    /// <summary>
    /// Gets a human-readable, auditable description of the decision.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Creates a permitted evaluation.
    /// </summary>
    /// <param name="statisticsAvailable">Whether the rolling statistics were available.</param>
    /// <param name="ratePercent">The measured abandonment rate percentage.</param>
    /// <param name="sampleSize">The number of live-answered calls measured.</param>
    /// <param name="description">An auditable description of why dialing is permitted.</param>
    /// <returns>A permitted <see cref="DialerAbandonmentEvaluation"/>.</returns>
    public static DialerAbandonmentEvaluation Permitted(
        bool statisticsAvailable,
        double ratePercent,
        long sampleSize,
        string description)
    {
        return new DialerAbandonmentEvaluation(true, statisticsAvailable, ratePercent, sampleSize, description);
    }

    /// <summary>
    /// Creates a suppressed evaluation.
    /// </summary>
    /// <param name="statisticsAvailable">Whether the rolling statistics were available.</param>
    /// <param name="ratePercent">The measured abandonment rate percentage.</param>
    /// <param name="sampleSize">The number of live-answered calls measured.</param>
    /// <param name="description">An auditable description of why dialing is suppressed.</param>
    /// <returns>A suppressed <see cref="DialerAbandonmentEvaluation"/>.</returns>
    public static DialerAbandonmentEvaluation Suppressed(
        bool statisticsAvailable,
        double ratePercent,
        long sampleSize,
        string description)
    {
        return new DialerAbandonmentEvaluation(false, statisticsAvailable, ratePercent, sampleSize, description);
    }
}
