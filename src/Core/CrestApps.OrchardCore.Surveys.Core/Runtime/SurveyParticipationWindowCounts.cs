namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Counts in each limit window.
/// </summary>
public sealed class SurveyParticipationWindowCounts
{
    /// <summary>
    /// Gets or sets the count in total (participation is kept for 45 days, so totals past that come from the responses
    /// of identified respondents).
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Gets or sets the count today.
    /// </summary>
    public int Day { get; set; }

    /// <summary>
    /// Gets or sets the count this week.
    /// </summary>
    public int Week { get; set; }

    /// <summary>
    /// Gets or sets the count this month.
    /// </summary>
    public int Month { get; set; }
}
