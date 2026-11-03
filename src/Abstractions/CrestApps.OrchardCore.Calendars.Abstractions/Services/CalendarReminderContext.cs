using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// An occurrence a reminder is raised for.
/// </summary>
public sealed class CalendarReminderContext
{
    /// <summary>
    /// Gets or sets the occurrence.
    /// </summary>
    public CalendarOccurrence Occurrence { get; set; }

    /// <summary>
    /// Gets or sets the occurrence's calendar.
    /// </summary>
    public Calendar Calendar { get; set; }

    /// <summary>
    /// Gets or sets how long before the start the reminder is raised, for a "starting soon" moment.
    /// </summary>
    public TimeSpan Offset { get; set; }
}
