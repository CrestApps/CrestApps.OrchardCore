namespace CrestApps.OrchardCore.Calendars.Models;

/// <summary>
/// What a user can do with a calendar. Each level includes the ones below it.
/// </summary>
public enum CalendarAccessLevel
{
    /// <summary>
    /// Nothing.
    /// </summary>
    None = 0,

    /// <summary>
    /// See when its events are busy, without titles or details.
    /// </summary>
    FreeBusy = 1,

    /// <summary>
    /// See its events and their details.
    /// </summary>
    Read = 2,

    /// <summary>
    /// Add, change and cancel its events.
    /// </summary>
    Write = 3,

    /// <summary>
    /// Change the calendar itself and who it is shared with.
    /// </summary>
    Manage = 4,
}
