namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// How many people are needed during a time of day, on a weekday or on one date.
/// </summary>
public sealed class CoverageRow
{
    /// <summary>
    /// Gets or sets the weekday, or <see langword="null"/> for a row on one date.
    /// </summary>
    public DayOfWeek? Day { get; set; }

    /// <summary>
    /// Gets or sets the date; a row on a date replaces the weekday rows of that day.
    /// </summary>
    public DateOnly? Date { get; set; }

    /// <summary>
    /// Gets or sets when the need starts, in the team's time zone.
    /// </summary>
    public TimeOnly Start { get; set; }

    /// <summary>
    /// Gets or sets when it ends; a time at or before the start runs past midnight.
    /// </summary>
    public TimeOnly End { get; set; }

    /// <summary>
    /// Gets or sets the fewest people needed.
    /// </summary>
    public int Minimum { get; set; }

    /// <summary>
    /// Gets or sets the number of people wanted.
    /// </summary>
    public int Target { get; set; }

    /// <summary>
    /// Gets or sets the most people useful, or <see langword="null"/> for no limit.
    /// </summary>
    public int? Maximum { get; set; }
}
