namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the rolling outbound-dialing outcomes a statistics provider reports for a dialer profile so
/// the abandonment policy can decide whether the profile stays within its configured cap.
/// </summary>
public sealed class DialerAbandonmentStatistics
{
    /// <summary>
    /// Gets or sets the number of calls a live person answered within the rolling window. This is the
    /// denominator of the abandonment rate.
    /// </summary>
    public long LiveAnswers { get; set; }

    /// <summary>
    /// Gets or sets the number of live-answered calls that were abandoned because no agent connected in
    /// time within the rolling window. This is the numerator of the abandonment rate.
    /// </summary>
    public long AbandonedCalls { get; set; }
}
