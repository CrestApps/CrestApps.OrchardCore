namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// How many responses each respondent may give.
/// </summary>
public sealed class RespondentLimits
{
    /// <summary>
    /// Gets or sets whether the limits count completed or started responses.
    /// </summary>
    public LimitCount Counts { get; set; }

    /// <summary>
    /// Gets or sets the most responses in total, such as quiz attempts.
    /// </summary>
    public int? Total { get; set; }

    /// <summary>
    /// Gets or sets the most responses a day.
    /// </summary>
    public int? PerDay { get; set; }

    /// <summary>
    /// Gets or sets the most responses a week; weeks start on the site's first day of the week.
    /// </summary>
    public int? PerWeek { get; set; }

    /// <summary>
    /// Gets or sets the most responses a month.
    /// </summary>
    public int? PerMonth { get; set; }

    /// <summary>
    /// Gets or sets the minutes a respondent must wait between responses.
    /// </summary>
    public int? CooldownMinutes { get; set; }

    /// <summary>
    /// Gets or sets how a respondent is recognized.
    /// </summary>
    public UniquenessMode Uniqueness { get; set; }
}
