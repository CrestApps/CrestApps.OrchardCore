namespace CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;

/// <summary>
/// Represents the call insights report: interaction volume, outcomes, handle time, and daily trend
/// over a reporting period.
/// </summary>
public sealed class CallInsightsReport
{
    /// <summary>
    /// Gets or sets the inclusive lower UTC bound of the reporting period.
    /// </summary>
    public DateTime FromUtc { get; set; }

    /// <summary>
    /// Gets or sets the inclusive upper UTC bound of the reporting period.
    /// </summary>
    public DateTime ToUtc { get; set; }

    /// <summary>
    /// Gets or sets the total number of interactions in the period.
    /// </summary>
    public long Total { get; set; }

    /// <summary>
    /// Gets or sets the number of inbound interactions.
    /// </summary>
    public long Inbound { get; set; }

    /// <summary>
    /// Gets or sets the number of outbound interactions.
    /// </summary>
    public long Outbound { get; set; }

    /// <summary>
    /// Gets or sets the number of interactions that were answered (connected to an agent).
    /// </summary>
    public long Answered { get; set; }

    /// <summary>
    /// Gets or sets the number of inbound interactions abandoned before an agent answered.
    /// </summary>
    public long Abandoned { get; set; }

    /// <summary>
    /// Gets or sets the number of interactions that failed.
    /// </summary>
    public long Failed { get; set; }

    /// <summary>
    /// Gets or sets the total talk time, in seconds, across all answered interactions.
    /// </summary>
    public double TotalTalkTimeSeconds { get; set; }

    /// <summary>
    /// Gets or sets the average handle time, in seconds, across all answered interactions.
    /// </summary>
    public double AverageHandleTimeSeconds { get; set; }

    /// <summary>
    /// Gets or sets the average speed of answer, in seconds, across all answered interactions.
    /// </summary>
    public double AverageSpeedOfAnswerSeconds { get; set; }

    /// <summary>
    /// Gets the answer rate (answered divided by total), between 0 and 1.
    /// </summary>
    public double AnswerRate
    {
        get
        {
            if (Total <= 0)
            {
                return 0d;
            }

            return (double)Answered / Total;
        }
    }

    /// <summary>
    /// Gets the abandonment rate (abandoned divided by inbound), between 0 and 1.
    /// </summary>
    public double AbandonmentRate
    {
        get
        {
            if (Inbound <= 0)
            {
                return 0d;
            }

            return (double)Abandoned / Inbound;
        }
    }

    /// <summary>
    /// Gets or sets the interaction volume grouped by channel.
    /// </summary>
    public IList<ContactCenterReportCount> ByChannel { get; set; } = [];

    /// <summary>
    /// Gets or sets the interaction volume grouped by communication-session status.
    /// </summary>
    public IList<ContactCenterReportCount> ByStatus { get; set; } = [];

    /// <summary>
    /// Gets or sets the daily interaction trend for the period.
    /// </summary>
    public IList<CallInsightsDailyPoint> Daily { get; set; } = [];
}
