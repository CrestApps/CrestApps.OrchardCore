namespace CrestApps.OrchardCore.Calendars.Models;

/// <summary>
/// What the calendars of a type support.
/// </summary>
[Flags]
public enum CalendarCapabilities
{
    /// <summary>
    /// Nothing beyond timed events.
    /// </summary>
    None = 0,

    /// <summary>
    /// Events can have attendees.
    /// </summary>
    Attendees = 1,

    /// <summary>
    /// Events can repeat.
    /// </summary>
    Recurrence = 2,

    /// <summary>
    /// Events can last whole days.
    /// </summary>
    AllDay = 4,

    /// <summary>
    /// The calendar counts toward its owner's and attendees' free/busy time.
    /// </summary>
    FreeBusy = 8,

    /// <summary>
    /// The calendar can be connected to an external calendar.
    /// </summary>
    ExternalSync = 16,

    /// <summary>
    /// The calendar can be published as a private iCalendar feed.
    /// </summary>
    Feed = 32,

    /// <summary>
    /// Everything.
    /// </summary>
    All = 63,
}
