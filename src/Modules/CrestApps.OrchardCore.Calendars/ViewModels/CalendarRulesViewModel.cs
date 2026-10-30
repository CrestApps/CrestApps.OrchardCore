namespace CrestApps.OrchardCore.Calendars.ViewModels;

/// <summary>
/// The Overlaps card of the calendar editor.
/// </summary>
public class CalendarRulesViewModel
{
    /// <summary>
    /// Gets or sets whether the calendar limits overlapping events.
    /// </summary>
    public bool LimitOverlaps { get; set; }

    /// <summary>
    /// Gets or sets how many busy events may overlap.
    /// </summary>
    public int Capacity { get; set; } = 1;
}
