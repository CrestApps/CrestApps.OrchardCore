namespace CrestApps.OrchardCore.Calendars.Models;

/// <summary>
/// Whether an event makes its calendar's owner and attendees busy.
/// </summary>
public enum CalendarTransparency
{
    /// <summary>
    /// The time is taken.
    /// </summary>
    Busy,

    /// <summary>
    /// The event is shown but leaves the time free.
    /// </summary>
    Free,
}
