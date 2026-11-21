namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// The time limit of a response.
/// </summary>
public sealed class SurveyTimingSettings
{
    /// <summary>
    /// Gets or sets the minutes a respondent has, or null for no limit.
    /// </summary>
    public int? TimeLimitMinutes { get; set; }

    /// <summary>
    /// Gets or sets the seconds allowed after the deadline for a page post in flight.
    /// </summary>
    public int GraceSeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether an unfinished response is submitted at its deadline.
    /// </summary>
    public bool AutoSubmitAtDeadline { get; set; } = true;
}
