namespace CrestApps.OrchardCore.Calendars.Models;

/// <summary>
/// How a calendar treats events that overlap.
/// </summary>
public enum CalendarConflictPolicy
{
    /// <summary>
    /// Events may overlap freely.
    /// </summary>
    AllowOverlaps,

    /// <summary>
    /// No more than the calendar's capacity of busy events may overlap at any moment.
    /// </summary>
    LimitConcurrent,
}
