namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// Hours on one day of a timesheet under one pay code.
/// </summary>
public sealed class TimesheetLine
{
    /// <summary>
    /// Gets or sets the line's id, for manual lines.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the day, in the worker's time.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets the pay code.
    /// </summary>
    public string PayCodeId { get; set; }

    /// <summary>
    /// Gets or sets the hours.
    /// </summary>
    public decimal Hours { get; set; }

    /// <summary>
    /// Gets or sets the activity.
    /// </summary>
    public string ActivityCodeId { get; set; }

    /// <summary>
    /// Gets or sets the job.
    /// </summary>
    public string Job { get; set; }

    /// <summary>
    /// Gets or sets where the line came from.
    /// </summary>
    public TimesheetLineSource Source { get; set; }

    /// <summary>
    /// Gets or sets the punches the hours came from.
    /// </summary>
    public IList<string> PunchIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the time-off requests or predictability pay entries the hours came from.
    /// </summary>
    public IList<string> SourceIds { get; set; } = [];

    /// <summary>
    /// Gets or sets a note.
    /// </summary>
    public string Note { get; set; }
}
