namespace CrestApps.OrchardCore.Scheduling.Models;

/// <summary>
/// Different hours on one date, or a day off.
/// </summary>
public sealed class SchedulingDateOverride
{
    /// <summary>
    /// Gets or sets the date.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Gets or sets whether the resource is off all day.
    /// </summary>
    public bool Closed { get; set; }

    /// <summary>
    /// Gets or sets when the day's hours start, as HH:mm, when not closed.
    /// </summary>
    public string Start { get; set; }

    /// <summary>
    /// Gets or sets when the day's hours end, as HH:mm, when not closed.
    /// </summary>
    public string End { get; set; }
}
